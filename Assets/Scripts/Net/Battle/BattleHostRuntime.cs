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
    [Tooltip("权威快照定频：0.0167s ≈ 60Hz（更密的权威位置 → 插值追上根运动加减速轨迹，Boss 滑步大幅减少）")]
    [SerializeField] private float tickInterval = 0.0167f;
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
    private BossBrain _boss;          // 主机上的 Boss（M11：采样进快照同步给客户端）
    private float _lastBossHp = -1f;  // Boss HP 对比基准（变化 → 广播伤害事件）
    private float _bossLogTimer;      // Boss 位移诊断节流
    private Vector3 _lastBossPos;
    private bool _hasLastBossPos;
    private int _nextRemoteId = 2;    // Remote 克隆的血条 id：主机玩家=1，克隆从 2 递增（防 HP 事件串到主机血条）
    private float _bossSyncLogTimer;  // [BossSync] 主机采样日志节流（与 [BossDiag] 的 _bossLogTimer 区分）
    private int _lastBossHash;        // [BossSync] 主机状态切换检测（变了就打一条）
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
        _server.OnBossHit += OnBossHitFromClient;   // 客机命中 Boss 上报 → 主机宽容判定

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
        LogBossState();   // [BossDiag] 主机 Boss 位置/位移/动画：定位"消失/飞远/拽回"

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
    /// 客机命中 Boss 上报（M11 伤害闭环）：客机本地命中了 Boss 幽灵，
    /// 上报位置/朝向/伤害 → 主机用客机报的位置对真 Boss 做宽容判定后扣血。
    /// 判定只查距离不查朝向：客机本地已经做过精确锥角判定，这里只是"防远距离凭空扣血"。
    /// </summary>
    private void OnBossHitFromClient(string attacker, MsgBossHit msg)
    {
        if (_boss == null) _boss = FindObjectOfType<BossBrain>();
        if (_boss == null || _boss.IsDead) return;

        // 网络延迟下客机位置与主机 Boss 位置可能差几米（客机本地命中时 Boss 还在动），放宽距离
        Vector3 atkPos = new Vector3(msg.PosX, msg.PosY, msg.PosZ);
        Vector3 toBoss = _boss.transform.position - atkPos;
        toBoss.y = 0f;
        float dist = toBoss.magnitude;
        if (dist > 5f)   // Boss 攻击命中范围 2.5~5m，加容差
        {
            // [BossHit] 主机拒绝：客机报的位置离 Boss 太远（客机在打空气/位置分叉）
            Debug.Log($"[BossHit] [Host] 收到 {attacker} 命中Boss，距离 {dist:F2}m 超容差 → 拒绝");
            return;
        }

        // 采纳客机伤害，加个上限兜底（防脚本改包一刀秒）
        float dmg = Mathf.Min(msg.Damage, 100f);
        _boss.TakeDamage(dmg, _localPlayer != null ? _localPlayer.gameObject : null);
        // [BossHit] 主机采纳：距离在容差内，扣真 Boss 血（血条变化由 CheckBossHp 广播）
        Debug.Log($"[BossHit] [Host] 收到 {attacker} 命中Boss，距离 {dist:F2}m → 采纳 damage={dmg:F1}");
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
    /// [BossDiag] 主机 Boss 状态诊断：每 0.5s 打印位置、位移量、当前动画 clip。
    /// 用于区分"跳变发生在主机侧（位移大/动画切换）"还是"客户端插值表现问题"。
    /// </summary>
    private void LogBossState()
    {
        if (_boss == null) return;
        _bossLogTimer -= Time.unscaledDeltaTime;
        if (_bossLogTimer > 0f) return;
        _bossLogTimer = 0.5f;

        var tr = _boss.transform;
        var animator = tr.GetComponent<Animator>();
        string clip = "?";
        if (animator != null)
        {
            var ci = animator.GetCurrentAnimatorClipInfo(0);
            if (ci.Length > 0) clip = ci[0].clip.name;
        }
        float moved = _hasLastBossPos ? Vector3.Distance(_lastBossPos, tr.position) : 0f;
        _lastBossPos = tr.position;
        _hasLastBossPos = true;

        Debug.Log($"[BossDiag] 位置 {tr.position} 位移 {moved:F2}m/0.5s 动画={clip} HP={_boss.CurrentHP}");
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
        pc.id = _nextRemoteId++;   // 血条 id 与主机玩家(1)错开：克隆的 HP 事件不串到主机血条
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

        // M11：Boss 也走主机权威快照（位置/动画/HP），客户端显示幽灵
        if (_boss == null) _boss = FindObjectOfType<BossBrain>();
        if (_boss != null)
        {
            items.Add(SampleBoss(_boss));
            LogBossSample(_tick);   // [BossSync] 主机采样日志
        }

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
        CheckBossHp();
    }

    /// <summary>
    /// M11：Boss 伤害/死亡事件同步（主机权威）。
    /// Boss 掉血由主机判定（Remote/本地玩家攻击主机上的 Boss），
    /// 这里对比 HP 变化后广播，客户端据此更新 Boss 血条/结算。
    /// </summary>
    private void CheckBossHp()
    {
        if (_boss == null) return;
        if (_lastBossHp < 0f)
        {
            _lastBossHp = _boss.CurrentHP;
            return;
        }

        if (_boss.CurrentHP < _lastBossHp - 0.01f)
        {
            float damage = _lastBossHp - _boss.CurrentHP;
            _server.BroadcastEvent(BattleEventType.Damage, "玩家", "Boss", damage, _boss.CurrentHP);
            if (_boss.IsDead)
                _server.BroadcastEvent(BattleEventType.Death, "玩家", "Boss", 0f, 0f);
        }
        _lastBossHp = _boss.CurrentHP;
    }

    /// <summary>Boss 采样：位置/朝向/移动参数/动画枚举/HP + 对峙 2D 树参数（与玩家同一份快照结构）</summary>
    private BattleSnapshotItem SampleBoss(BossBrain boss)
    {
        var tr = boss.transform;
        var animator = tr.GetComponent<Animator>();
        return new BattleSnapshotItem
        {
            Name = "Boss",
            PosX = tr.position.x,
            PosY = tr.position.y,
            PosZ = tr.position.z,
            RotY = tr.eulerAngles.y,
            // Boss 控制器没有 "Movement" 参数（那是玩家的），用 root motion 实际速度
            MoveSpeed = animator != null ? animator.velocity.magnitude : 0f,
            Anim = MapBossAnim(boss, animator),
            HP = boss.CurrentHP,
            MaxHP = boss.MaxHP,
            // Boss 动画由行为树 Trigger 驱动，状态名不固定（Attack4 等）——
            // 直接传当前状态 hash，客户端 CrossFade 到同名状态（两端同一 Animator Controller）
            AnimHash = animator != null ? animator.GetCurrentAnimatorStateInfo(0).shortNameHash : 0,
            // 动画相位（客户端锁步用）：两端从同一进度继续播，杜绝"动作滞后/滑步"
            BossNormalizedTime = animator != null ? animator.GetCurrentAnimatorStateInfo(0).normalizedTime : 0f,
            // 对峙 2D 树参数（客户端回放）：BossMotor 阻尼写入 SpeedX/SpeedY，IsSolo/IsMoving 进/出对峙
            BossSpeedX = animator != null ? animator.GetFloat("SpeedX") : 0f,
            BossSpeedY = animator != null ? animator.GetFloat("SpeedY") : 0f,
            BossIsSolo = animator != null && animator.GetBool("IsSolo"),
            BossIsMoving = animator != null && animator.GetBool("IsMoving"),
        };
    }

    /// <summary>Boss 动画枚举映射：按当前动画 clip 名关键词判断（Idle/Attack/Hit/Dash/Run/Dead）</summary>
    private BattleAnimState MapBossAnim(BossBrain boss, Animator animator)
    {
        if (boss.IsDead) return BattleAnimState.Dead;
        if (animator == null) return BattleAnimState.Idle;

        string clip = "";
        var ci = animator.GetCurrentAnimatorClipInfo(0);
        if (ci.Length > 0) clip = ci[0].clip.name;

        if (clip.Contains("Hit")) return BattleAnimState.Hit;
        if (clip.Contains("Attack") || clip.Contains("Atk")) return BattleAnimState.Attack;
        if (clip.Contains("Dash")) return BattleAnimState.Dash;
        if (clip.Contains("Walk") || clip.Contains("Run") || clip.Contains("Move")) return BattleAnimState.Run;
        return BattleAnimState.Idle;
    }

    /// <summary>
    /// [BossSync] 主机侧 Boss 状态日志：
    ///   状态切换（不节流，变了就打）→ 和客户端 [Clnt] 的状态切换对比，看动作延迟；
    ///   周期采样（0.5s）→ 和客户端 [Clnt] 同 tick 对比位置/相位/对峙参数。
    /// </summary>
    private void LogBossSample(int tick)
    {
        var animator = _boss != null ? _boss.GetComponent<Animator>() : null;
        if (animator == null) return;
        var st = animator.GetCurrentAnimatorStateInfo(0);

        // 状态切换：不节流（每 tick 都查），变了就打一条
        if (st.shortNameHash != _lastBossHash && st.shortNameHash != 0)
        {
            _lastBossHash = st.shortNameHash;
            Debug.Log($"[BossSync] [Host] 状态切换 hash={st.shortNameHash} time={st.normalizedTime:F2}");
        }

        // 周期采样：0.5s 一条
        _bossSyncLogTimer -= Time.unscaledDeltaTime;
        if (_bossSyncLogTimer > 0f) return;
        _bossSyncLogTimer = 0.5f;

        float sx = animator.GetFloat("SpeedX");
        float sy = animator.GetFloat("SpeedY");
        bool solo = animator.GetBool("IsSolo");
        bool move = animator.GetBool("IsMoving");
        Debug.Log($"[BossSync] [Host] tick={tick} | pos={_boss.transform.position:F1} " +
                  $"hash={st.shortNameHash} time={st.normalizedTime:F2} " +
                  $"sx={sx:F2} sy={sy:F2} solo={solo} move={move} hp={_boss.CurrentHP:F0}");
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
