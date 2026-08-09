using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// 房主胶水层（主机权威）— 跑在房主进程，把 BattleServer 和游戏对象接起来。
///
/// 为什么需要它：BattleServer 是纯 C#，不认识 PlayerController；本脚本两头都认识，当接线员——
///   服务器 → 游戏对象：OnInput 到达 → 找到对应远端角色 → RemoteInputProvider.Apply（遥控模拟）；
///   游戏对象 → 服务器：每 50ms 采样所有角色状态 → server.Tick 广播快照（主机权威）；
///   HP 变化 → 广播伤害/死亡事件。
///
/// 房主本地玩家由场景里的 PlayerSpawnPoint 生成（本脚本监听 PlayerSpawned 拿到引用），
/// 被邀请者由本脚本按加入事件动态生成（克隆角色 prefab + BindRemoteInput）。
/// </summary>
public class BattleHostRuntime : MonoBehaviour
{
    [Header("战斗服务器")]
    [Tooltip("战斗监听端口（大厅 JoinRoom 消息约定 7778）")]
    [SerializeField] private int port = 7778;
    [Tooltip("权威快照定频：0.033s ≈ 30Hz（更密的权威位置 → 客户端纠偏更及时，偏移更小）")]
    [SerializeField] private float tickInterval = 0.033f;
    [Tooltip("远端输入超时（秒）：超过该时间没收到某客户端的输入就清零它的移动（防漂移）")]
    [SerializeField] private float inputTimeout = 0.2f;

    [Header("远端角色生成")]
    [Tooltip("被邀请者角色 prefab 的地址（与 PlayerSpawnPoint 默认一致）")]
    [SerializeField] private string playerPrefabPath = "Prefabs/安比";
    [Tooltip("远端角色相对出生点的排列偏移，按加入顺序依次排开")]
    [SerializeField] private Vector3 remoteSpawnOffset = new Vector3(2f, 0f, 0f);

    private BattleServer _server;
    private readonly Dictionary<string, PlayerController> _remotePlayers = new();
    private readonly Dictionary<string, float> _lastHp = new();   // 名字 → 上次快照时的 HP（用于检测伤害）
    private readonly Dictionary<string, float> _lastInputTime = new();   // 名字 → 最后收到输入的时间（防漂移）
    private readonly Dictionary<string, Vector3> _pendingRemoteSpawns = new();   // 已加入但 Remote 未生成：快照占位
    private readonly Dictionary<string, Vector3> _lastClientPos = new();   // 名字 → 客户端上报的本地位置（初始同步）

    private PlayerController _localPlayer;
    private string _hostName = "Host";
    private Transform _spawnPoint;
    private Vector3[] _spawnPoints;   // 槽位 → 出生点（服务器权威，JoinAck 下发，Remote 生成统一用它）
    private float _tickAccum;
    private int _tick;
    private float _syncLogTimer;   // [SyncDebug] 主机视角偏差日志节流
    private float _inputLogTimer;   // 诊断日志节流
    private float _noInputCheckTimer;
    private readonly Dictionary<string, Vector3> _noInputLastPos = new();   // 无输入检测：上次采样位置

    private void Awake()
    {
        _spawnPoint = FindObjectOfType<PlayerSpawnPoint>()?.transform;
    }

    /// <summary>测试面板在 Start 前调用：显式指定房主名字（不指定时回退到大厅注册名）</summary>
    public void SetHostName(string name) => _hostName = string.IsNullOrEmpty(name) ? "Host" : name;

    private async void Start()
    {
        // 房主名字：面板显式指定过就用指定值；否则用大厅注册名；再兜底 "Host"
        if (_hostName == "Host" && LobbyClientService.HasInstance && !string.IsNullOrEmpty(LobbyClientService.Instance.MyName))
            _hostName = LobbyClientService.Instance.MyName;

        _server = new BattleServer();
        _server.SetHostName(_hostName);
        _server.OnInput += OnServerInput;           // 客户端输入 → 遥控远端角色
        _server.OnPlayerJoined += OnPlayerJoined;   // 有人加入 → 生成远端角色
        _server.OnPlayerLeft += OnPlayerLeft;       // 有人离开 → 销毁远端角色

        // 从大厅进入：房主名 + 成员表用大厅 JoinRoom 信息（槽位按大厅顺序分配）
        if (BattleSessionState.FromLobby)
        {
            _hostName = BattleSessionState.HostName;
            _server.SetHostName(_hostName);
            _server.PreRegister(BattleSessionState.MemberNames);
            Debug.Log($"[BattleHost] 来自大厅房间 {BattleSessionState.RoomId}，成员 [{string.Join(",", BattleSessionState.MemberNames)}]");
        }

        // 服务器权威出生点：以场景 PlayerSpawnPoint 为基准，按槽位 x+2 递增，下发给客户端
        BuildSpawnPoints();

        if (!_server.Start(port))
        {
            Debug.LogError($"[BattleHost] 战斗服务器启动失败 :{port}（可能已被占用，请检查是否有第二个房主进程）");
            Destroy(gameObject);
            return;
        }
        Debug.Log($"[BattleFlow] 战斗服务器已启动 :{port}，房主={_hostName}，等待客户端加入");

        // 本地玩家可能已经由 PlayerSpawnPoint 生成（事件早于本脚本启动），先找一轮兜底
        BindLocalPlayer(FindLocalPlayer());
        EventBus.Subscribe<GameObject>(GameEvents.PlayerSpawned, OnPlayerSpawned);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<GameObject>(GameEvents.PlayerSpawned, OnPlayerSpawned);
        _server?.Stop();
    }

    private void Update()
    {
        _server?.Poll();   // 驱动收包队列 → 消息路由 → 事件

        CheckInputTimeout();   // 输入超时清零：客户端失联/失焦时停住角色，别漂移
        CheckNoInputMovement();   // 诊断：无输入却有位移 → 警告（定位"自己向前移动"）
        LogRemoteInputSample();   // 诊断：周期性打印远端输入，定位"主机眼里自己向前移动"
        LogSyncDrift();   // [SyncDebug] 主机视角偏差：客户端上报位置 vs Remote 实际位置

        // 用真实时间推进快照节拍：时停（HitPause）不暂停网络同步，否则客户端会卡在旧状态
        _tickAccum += Time.unscaledDeltaTime;
        if (_tickAccum >= tickInterval)
        {
            _tickAccum -= tickInterval;
            TickOnce();
        }
    }

    // ================= 本地玩家登记 =================

    private PlayerController FindLocalPlayer()
    {
        foreach (var pc in FindObjectsOfType<PlayerController>())
            if (pc != null && !pc.IsRemote)
                return pc;
        return null;
    }

    private void OnPlayerSpawned(GameObject go)
    {
        var pc = go != null ? go.GetComponent<PlayerController>() : null;
        if (pc != null && !pc.IsRemote)
            BindLocalPlayer(pc);
    }

    private void BindLocalPlayer(PlayerController pc)
    {
        if (pc == null) return;
        _localPlayer = pc;
        _lastHp[_hostName] = pc.CurrentHP;
        Debug.Log($"[BattleHost] 房主本地玩家就绪：{_hostName}");
    }

    // ================= 服务器事件 → 游戏对象 =================

    /// <summary>客户端输入到达：把输入喂给对应远端角色的 RemoteInputProvider（M9 的完整体现）</summary>
    private void OnServerInput(string name, BattleInputState input)
    {
        _lastInputTime[name] = Time.unscaledTime;
        // 记录客户端本地位置：Remote 生成/占位对齐用（初始同步以客户端位置为起点）
        var clientPos = new Vector3(input.PosX, input.PosY, input.PosZ);
        _lastClientPos[name] = clientPos;
        if (_pendingRemoteSpawns.ContainsKey(name))
            _pendingRemoteSpawns[name] = clientPos;   // 占位快照跟随客户端位置 → 首帧对齐无跳变
        // 诊断：确认攻击/闪避边沿是否到达主机（攻击同步排查）
        if ((input.Flags & BattleInputFlags.Attack) != 0)
            Debug.Log($"[BattleHost] 收到 {name} 攻击输入（Attack）");
        if ((input.Flags & BattleInputFlags.Dash) != 0)
            Debug.Log($"[BattleHost] 收到 {name} 闪避输入（Dash）");
        if (!_remotePlayers.TryGetValue(name, out var pc)) return;
        if (pc == null) return;   // 防御：角色可能已被销毁但字典没清
        if (pc.Input is RemoteInputProvider remote)
            remote.Apply(input);
    }

    /// <summary>
    /// 输入超时检测：客户端停止上报（失焦/卡顿/断连）超过 inputTimeout 后，
    /// 把该玩家的 RemoteInputProvider 清零，防止"保留最后输入一直走"。
    /// 客户端恢复上报后自动继续（Apply 会覆盖新输入）。
    /// </summary>
    private void CheckInputTimeout()
    {
        float now = Time.unscaledTime;
        foreach (var kv in _remotePlayers)
        {
            if (!_lastInputTime.TryGetValue(kv.Key, out var last)) continue;
            if (now - last <= inputTimeout) continue;

            if (kv.Value != null && kv.Value.Input is RemoteInputProvider remote)
                remote.ClearInput();
        }
    }

    /// <summary>
    /// 诊断日志（定位"自己向前移动"）：每 0.5s 打印一次所有远端玩家的当前输入。
    /// move≠0 = 客户端在持续上报移动（输入语义/时序问题）；
    /// move=0 却仍在走 = Remote 角色模拟/动画残留问题。
    /// </summary>
    private void LogRemoteInputSample()
    {
        _inputLogTimer -= Time.unscaledDeltaTime;
        if (_inputLogTimer > 0f || _remotePlayers.Count == 0) return;
        _inputLogTimer = 0.5f;

        foreach (var kv in _remotePlayers)
        {
            var remote = kv.Value != null ? kv.Value.Input as RemoteInputProvider : null;
            if (remote == null) continue;
            Debug.Log($"[BattleHost] 输入采样 {kv.Key}: move=({remote.MoveX:F2},{remote.MoveZ:F2}) sprint={remote.SprintHeld}");
        }
    }

    /// <summary>
    /// [SyncDebug] 主机视角偏差：客户端上报的本地位置 vs 主机模拟的 Remote 位置。
    /// 这是"主机看到的客户端"和"客户端看到的自己"的差距，每 0.5s 打一次。
    /// </summary>
    private void LogSyncDrift()
    {
        _syncLogTimer -= Time.unscaledDeltaTime;
        if (_syncLogTimer > 0f || _remotePlayers.Count == 0) return;
        _syncLogTimer = 0.5f;

        foreach (var kv in _remotePlayers)
        {
            if (kv.Value == null || !_lastClientPos.TryGetValue(kv.Key, out var cp)) continue;
            float d = Vector3.Distance(cp, kv.Value.transform.position);
            Debug.Log($"[SyncDebug] 主机视角偏差 {d:F2}m | 客户端上报 {cp} vs Remote {kv.Value.transform.position}");
        }
    }

    /// <summary>
    /// 诊断检测（定位"自己向前移动"）：每 0.2s 检查一次所有远端角色——
    /// 如果 RemoteInputProvider 的输入为 0，但角色在 0.2s 内位移超过 0.1m（≈0.5m/s），
    /// 说明"没有输入却有位移"，打印警告（带状态机/动画诊断）。
    /// 已排除攻击/受击/闪避动画（这些是正常 root motion 位移）；
    /// 仍触发的动画 = 尚未识别的位移来源，需要继续查。
    /// </summary>
    private void CheckNoInputMovement()
    {
        _noInputCheckTimer -= Time.unscaledDeltaTime;
        if (_noInputCheckTimer > 0f || _remotePlayers.Count == 0) return;
        _noInputCheckTimer = 0.2f;

        foreach (var kv in _remotePlayers)
        {
            var pc = kv.Value;
            var remote = pc != null ? pc.Input as RemoteInputProvider : null;
            if (pc == null || remote == null) continue;

            // 攻击/受击/闪避动画自带 root motion 位移（突进/击退），是正常战斗表现，跳过
            if (pc.Action != null &&
                (pc.Action.CurrentState is ATKingState || pc.Action.CurrentState is HitState))
                continue;
            if (pc.Locomotion != null && pc.Locomotion.CurrentState is DashingState)
                continue;

            bool hasInput = remote.MoveX * remote.MoveX + remote.MoveZ * remote.MoveZ > 0.0001f;
            if (hasInput)
            {
                _noInputLastPos[kv.Key] = pc.transform.position;   // 有输入：只刷新基准，不判
                continue;
            }

            if (_noInputLastPos.TryGetValue(kv.Key, out var last))
            {
                float moved = Vector3.Distance(pc.transform.position, last);
                // 阈值 0.5m/0.2s（≈2.5m/s）：过滤动画收尾/滑步的正常位移
                // （Run_End/Walk 衰减、攻击收招等 <0.5m），只抓"持续高速漂移"。
                if (moved > 0.5f)
                {
                    // 状态诊断：状态机状态 + Movement 参数 + 当前动画 clip 名，用于识别位移来源
                    string loco = pc.Locomotion != null ? pc.Locomotion.CurrentState?.GetType().Name : "?";
                    string action = pc.Action != null ? pc.Action.CurrentState?.GetType().Name : "?";
                    float movement = pc.Animator != null ? pc.Animator.GetFloat("Movement") : -1f;
                    string clip = "?";
                    if (pc.Animator != null)
                    {
                        var clipInfo = pc.Animator.GetCurrentAnimatorClipInfo(0);
                        if (clipInfo.Length > 0) clip = clipInfo[0].clip.name;
                    }
                    Debug.LogWarning(
                        $"[BattleHost] 无输入却有位移：{kv.Key} 0.2s 移动 {moved:F3}m | " +
                        $"Loco={loco} Action={action} Movement={movement:F2} 动画={clip} 位置={pc.transform.position}");
                }
            }
            _noInputLastPos[kv.Key] = pc.transform.position;
        }
    }

    /// <summary>新玩家加入：克隆角色 prefab，绑定远端输入，放进世界</summary>
    private async void OnPlayerJoined(string name)
    {
        if (_remotePlayers.TryGetValue(name, out var old))
        {
            Destroy(old.gameObject);   // 防御：同名重连先清旧角色
            _remotePlayers.Remove(name);
        }

        // 先占位：快照立即包含该玩家，客户端第一帧就能对齐。
        int slot = _remotePlayers.Count + 1;
        Vector3 pos = _spawnPoints != null && slot < _spawnPoints.Length
            ? _spawnPoints[slot]
            : (_spawnPoint != null ? _spawnPoint.position : Vector3.zero);
        if (_lastClientPos.TryGetValue(name, out var cp))
            pos = cp;
        _pendingRemoteSpawns[name] = pos;

        var prefab = await ResourceManager.Instance.LoadAsync<GameObject>(playerPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[BattleHost] 找不到角色 prefab：{playerPrefabPath}，无法生成 {name} 的远端角色");
            _pendingRemoteSpawns.Remove(name);
            return;
        }

        var go = Instantiate(prefab, pos, Quaternion.identity);
        go.name = $"Remote_{name}";

        var pc = go.GetComponent<PlayerController>();
        if (pc == null)
        {
            Destroy(go);
            Debug.LogWarning($"[BattleHost] 角色 prefab 没有 PlayerController，无法遥控 {name}");
            return;
        }

        pc.BindRemoteInput(new RemoteInputProvider());   // 换成"网络输入源"→ 被遥控
        _remotePlayers[name] = pc;
        _lastHp[name] = pc.CurrentHP;
        _pendingRemoteSpawns.Remove(name);   // 真实角色就绪，占位移除
        Debug.Log($"[BattleHost] 远端角色生成：{name}（槽位 {slot}）");
    }

    /// <summary>
    /// 槽位出生点列表：基准 = 场景 PlayerSpawnPoint，槽位 i → 基准 + offset×i。
    /// 传给 BattleServer 下发给客户端（服务器权威，客户端不推算）；Remote 生成也用同一份。
    /// </summary>
    private void BuildSpawnPoints()
    {
        Vector3 basePos = _spawnPoint != null ? _spawnPoint.position : Vector3.zero;
        _spawnPoints = new Vector3[4];   // 最多 4 人
        var flat = new List<float[]>();
        for (int i = 0; i < _spawnPoints.Length; i++)
        {
            _spawnPoints[i] = basePos + remoteSpawnOffset * i;
            flat.Add(new[] { _spawnPoints[i].x, _spawnPoints[i].y, _spawnPoints[i].z });
        }
        _server.SetSpawnPoints(flat);
        Debug.Log($"[BattleFlow] 槽位出生点：0={_spawnPoints[0]} 1={_spawnPoints[1]}");
    }

    private void OnPlayerLeft(string name)
    {
        if (_remotePlayers.TryGetValue(name, out var pc))
        {
            if (pc != null) Destroy(pc.gameObject);
            _remotePlayers.Remove(name);
            _lastHp.Remove(name);
            _lastInputTime.Remove(name);
            _lastClientPos.Remove(name);
            Debug.Log($"[BattleHost] 远端角色销毁：{name}");
        }
    }

    // ================= 游戏对象 → 服务器（权威快照） =================

    private void TickOnce()
    {
        var items = new List<BattleSnapshotItem>();
        if (_localPlayer != null)
            items.Add(SamplePlayer(_localPlayer, _hostName));

        // 占位玩家：Remote 还在异步加载，先按出生点发 Idle/满血快照，
        // 客户端第一帧就能收到"我=出生点"并对齐，避免开局错位。
        foreach (var kv in _pendingRemoteSpawns)
        {
            items.Add(new BattleSnapshotItem
            {
                Name = kv.Key,
                PosX = kv.Value.x,
                PosY = kv.Value.y,
                PosZ = kv.Value.z,
                RotY = 0f,
                MoveSpeed = 0f,
                Anim = BattleAnimState.Idle,
                HP = 100f,
                MaxHP = 100f,
                Placeholder = true,   // 占位快照：Remote 未生成，客户端不能据此解锁
            });
        }

        // 防御 + 排查：遍历时若发现已销毁的角色，说明它被外部销毁了（网络异步竞态），
        // 打印一条日志供定位，然后从字典移除，避免每帧 MissingReference 刷屏。
        List<string> dead = null;
        foreach (var kv in _remotePlayers)
        {
            if (kv.Value == null)
            {
                if (dead == null) dead = new List<string>();
                dead.Add(kv.Key);
                Debug.LogWarning($"[BattleHost] 发现已销毁的远端角色：{kv.Key}（疑被外部销毁，请检查日志）");
                continue;
            }
            items.Add(SamplePlayer(kv.Value, kv.Key));
        }
        if (dead != null)
            foreach (var name in dead)
                _remotePlayers.Remove(name);

        _server.Tick(++_tick, items);
        CheckHpChanges();
    }

    /// <summary>把一名角色采样成快照条目：位置/朝向/移动参数/动作枚举/HP</summary>
    private BattleSnapshotItem SamplePlayer(PlayerController pc, string name)
    {
        var tr = pc.transform;
        return new BattleSnapshotItem
        {
            Name = name,
            PosX = tr.position.x,
            PosY = tr.position.y,
            PosZ = tr.position.z,
            RotY = tr.eulerAngles.y,
            MoveSpeed = pc.Animator != null ? pc.Animator.GetFloat("Movement") : 0f,
            Anim = BattleAnimMapper.FromFlags(
                pc.Action.CurrentState is DeathState,
                pc.Action.CurrentState is HitState,
                pc.Action.CurrentState is ATKingState,
                pc.Locomotion.CurrentState is DashingState,
                pc.Locomotion.CurrentState is SprintState,
                pc.Locomotion.CurrentState is RunState,
                pc.Locomotion.CurrentState is TurnBackState),
            HP = pc.CurrentHP,
            MaxHP = pc.MaxHP,
        };
    }

    /// <summary>轮询对比 HP：下降 → 广播伤害事件；归零 → 广播死亡事件</summary>
    private void CheckHpChanges()
    {
        if (_localPlayer != null) CheckPlayerHp(_hostName, _localPlayer);
        foreach (var kv in _remotePlayers)
            CheckPlayerHp(kv.Key, kv.Value);
    }

    private void CheckPlayerHp(string name, PlayerController pc)
    {
        if (pc == null) return;
        if (!_lastHp.TryGetValue(name, out var last))
        {
            _lastHp[name] = pc.CurrentHP;
            return;
        }

        if (pc.CurrentHP < last - 0.01f)
        {
            float damage = last - pc.CurrentHP;
            // from 简化写 "Boss"：正式做可在 PlayerController.TakeDamage 里把 attacker 名字传进来
            _server.BroadcastEvent(BattleEventType.Damage, "Boss", name, damage, pc.CurrentHP);
            if (pc.CurrentHP <= 0f)
                _server.BroadcastEvent(BattleEventType.Death, "Boss", name, 0f, 0f);
        }
        _lastHp[name] = pc.CurrentHP;
    }
}
