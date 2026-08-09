using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// 客户端胶水层 — 被邀请者进程，把 BattleClient 和本地游戏对象接起来。
///
/// 为什么需要它（四根线）：
///   输入 → 服务器：订阅本地 LocalInputProvider.OnEdge 即时上报边沿；每 50ms 定频上报摇杆；
///   服务器 → 自己：快照纠偏（本地预测优先，偏差大才拉回）+ 伤害事件应用（以主机 HP 为准）；
///   服务器 → 别人：快照驱动幽灵插值/动画；离开时销毁幽灵；
///   另外：客户端不模拟 Boss（M11 前隐藏本地 Boss，避免双端各打各的导致 HP 分叉）。
/// </summary>
public class BattleClientRuntime : MonoBehaviour
{
    [Header("房主地址")]
    [Tooltip("房主 IP（局域网联机时填房主机器的局域网 IP）")]
    [SerializeField] private string hostIp = "127.0.0.1";
    [Tooltip("房主战斗端口（大厅 JoinRoom 约定 7778）")]
    [SerializeField] private int port = 7778;
    [Tooltip("本地预测纠偏阈值：与主机误差超过该距离才被拉回（防每次快照都抖）")]
    [SerializeField] private float reconcileThreshold = 0.15f;
    [Tooltip("解锁后入场缓冲（秒）：链路稳定前忽略攻击/闪避，防刚落地按攻击造成位置错位")]
    [SerializeField] private float spawnInputGate = 0.5f;
    [Tooltip("平滑拉回速度（m/s）：纠偏时不瞬移，按此速度向权威位置靠拢")]
    [SerializeField] private float pullSpeed = 20f;
    [Tooltip("是否执行本地纠偏（单进程同时开主机+客户端时关掉：本地玩家就是房主，没有独立客户端模拟）")]
    [SerializeField] private bool reconcileLocal = true;
    [Tooltip("单进程演示模式（同进程开主机+客户端）：不生成幽灵、不应用伤害事件，避免鬼影跟随和血量串台")]
    [SerializeField] private bool singleProcessDemo;
    [Tooltip("幽灵角色 prefab 地址（与 PlayerSpawnPoint 默认一致）")]
    [SerializeField] private string playerPrefabPath = "Prefabs/安比";
    [Tooltip("是否隐藏本地 Boss（单进程同时开主机+客户端测试时设 false，让主机管 Boss）")]
    [SerializeField] private bool hideLocalBoss = true;
    [Tooltip("按槽位的出生偏移（与 BattleHostRuntime.remoteSpawnOffset 保持一致：两端同一玩家的出生点必须相同）")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(2f, 0f, 0f);

    private BattleClient _client;
    private readonly Dictionary<string, BattleGhostInterpolator> _ghosts = new();
    private BattleGhostInterpolator _bossGhost;   // M11：客户端 Boss 幽灵（按主机快照显示）
    private float _bossMaxHp;                     // Boss 最大血量（从快照更新，事件用）
    private PlayerController _localPlayer;
    private string _myName = "Guest";
    private bool _bossHandled;
    private int _mySlot;             // 加入回执里给的槽位（0=房主，1+ 按加入顺序）
    private bool _slotApplied;       // 出生点是否已按槽位搬移（只搬一次）
    private int _appliedSlot = -1;   // 已应用过出生点的槽位（槽位变化时要重新搬）
    private Vector3 _slotSpawnPos;   // 槽位出生点（首帧对齐前钉住用）
    private Vector3 _serverSpawn;    // 服务器下发的出生点（JoinAck）
    private bool _serverSpawnSet;    // 是否已收到服务器出生点
    private bool _waitFirstSelfSnap; // 加入后等第一帧"自己"快照：期间锁移动，避免开局错位
    private float _inputGateTimer;   // 入场缓冲剩余时间（解锁后短暂忽略攻击/闪避）
    private Vector3 _lastSelfTarget; // 最后的主机快照位置（[SyncDebug] 偏差采样用）
    private float _syncLogTimer;     // [SyncDebug] 偏差日志节流（测试用，低频）
    private float _inputSendTimer;   // 移动输入上报节流（30Hz，减半 GC 分配；边沿事件仍即时）

    public bool Connected => _client != null && _client.Connected;
    public string MyName => _myName;
    public int GhostCount => _ghosts.Count;

    /// <summary>测试面板在 Start 前调用：单进程同时开主机时设为 false，让主机管 Boss</summary>
    public void SetHideLocalBoss(bool hide) => hideLocalBoss = hide;

    /// <summary>测试面板在 Start 前调用：单进程同时开主机时关掉纠偏（本地玩家=房主，没有独立模拟）</summary>
    public void SetReconcileLocal(bool enable) => reconcileLocal = enable;

    /// <summary>测试面板在 Start 前调用：单进程演示时不开幽灵/伤害应用（没有第二个独立玩家）</summary>
    public void SetSingleProcessDemo(bool enable) => singleProcessDemo = enable;

    /// <summary>由 BattleDevKit（测试面板）创建时调用：在 Start 之前设置连接参数</summary>
    public void Configure(string ip, int portValue, string name)
    {
        hostIp = ip;
        port = portValue;
        _myName = name;
    }

    private void Start()
    {
        _client = new BattleClient();
        _client.OnJoined += OnJoined;
        _client.OnSnapshot += OnSnapshot;
        _client.OnBattleEvent += OnBattleEvent;
        _client.OnPlayerLeft += OnPlayerLeft;
        _client.OnDisconnected += () => Debug.Log("[BattleClient] 与房主断开");
        _client.OnError += msg => Debug.LogWarning($"[BattleClient] {msg}");
        _client.Connect(hostIp, port, _myName);

        // 本地玩家可能已由 PlayerSpawnPoint 生成（事件早于本脚本启动），先找一轮兜底
        BindLocalPlayer(FindLocalPlayer());
        EventBus.Subscribe<GameObject>(GameEvents.PlayerSpawned, OnPlayerSpawned);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<GameObject>(GameEvents.PlayerSpawned, OnPlayerSpawned);
        _client?.Disconnect();
    }

    private void Update()
    {
        _client?.Poll();   // 驱动收包队列 → 事件

        // 加入后首帧对齐前：把本地玩家钉在槽位出生点，不上报移动——
        // 否则客户端本地先跑、主机 Remote 后出生，开局就错位几米。
        if (_waitFirstSelfSnap)
        {
            if (_localPlayer != null && _slotApplied)
                _localPlayer.transform.position = _slotSpawnPos;
            return;
        }

        // 入场缓冲：解锁后 spawnInputGate 秒内关闭攻击/闪避边沿
        // （本地不执行 + 不上报，等 Remote 初始状态稳定再开放瞬时行为）
        if (_inputGateTimer > 0f)
        {
            _inputGateTimer -= Time.unscaledDeltaTime;
            if (_localPlayer != null && _localPlayer.Input is LocalInputProvider local)
                local.GateEdges = true;
        }
        else if (_localPlayer != null && _localPlayer.Input is LocalInputProvider local)
        {
            local.GateEdges = false;
        }

        // 移动输入 30Hz 定频上报（减半 GC 分配，30Hz 对移动同步足够；边沿事件仍由 OnEdge 即时发）
        _inputSendTimer -= Time.unscaledDeltaTime;
        if (_inputSendTimer <= 0f && _localPlayer != null && Connected)
        {
            _inputSendTimer = 0.033f;
            var flags = _localPlayer.Input != null && _localPlayer.Input.SprintHeld
                ? BattleInputFlags.Sprint : BattleInputFlags.None;
            Vector2 world = WorldMove();
            var pos = _localPlayer.transform.position;
            _client.SendInput(world.x, world.y, flags, pos.x, pos.y, pos.z);
        }

        // [SyncDebug] 偏差采样（测试用，低频不刷屏）：每 0.5s 打一次"本地 vs 主机"距离
        _syncLogTimer -= Time.unscaledDeltaTime;
        if (_syncLogTimer <= 0f && !_waitFirstSelfSnap && _localPlayer != null && _lastSelfTarget != Vector3.zero)
        {
            _syncLogTimer = 0.5f;
            float drift = Vector3.Distance(_localPlayer.transform.position, _lastSelfTarget);
            Debug.Log($"[SyncDebug] 偏差 {drift:F2}m | 本地 {_localPlayer.transform.position} vs 主机 {_lastSelfTarget}");
        }

        // M11：客户端不模拟 Boss——把本地 Boss 幽灵化（销毁 AI/碰撞，保留 Animator 按快照演）
        if (!_bossHandled && !singleProcessDemo)
        {
            var boss = FindObjectOfType<BossBrain>();
            if (boss != null)
            {
                GhostifyBoss(boss);
                _bossHandled = true;
            }
        }
    }

    // ================= 本地玩家登记 + 输入上报 =================

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
        if (pc == null || _localPlayer != null) return;   // 防重复绑定（PlayerSpawned 事件可能多次）
        _localPlayer = pc;
        ApplyServerSpawn();   // 本地玩家可能晚于 OnJoined 生成（异步），绑定后再补搬一次

        // 输入边沿 → 即时上报（攻击/闪避按下不等 50ms）：M9 预留的 OnEdge 钩子在这里接上
        if (pc.Input is LocalInputProvider local)
            local.OnEdge += SendEdgeInput;
    }

    /// <summary>
    /// 非 host 的本地玩家生成：PlayerSpawnPoint 已跳过，由这里按服务器下发的出生点生成。
    /// SpawnPlayerAt 内部会发 PlayerSpawned 事件 → OnPlayerSpawned → BindLocalPlayer。
    /// </summary>
    private async void EnsureLocalPlayer()
    {
        if (_localPlayer != null || singleProcessDemo) return;   // 已有玩家 / 单进程演示（房主就是本地玩家）

        // 防重复生成：PlayerSpawnPoint 的"跳过"判断若因时序没生效，场景可能已有一个本地玩家，
        // 直接绑定它，不重复生成第二个（否则两个玩家对象 → 日志/纠偏读到不同对象）。
        var existing = FindLocalPlayer();
        if (existing != null)
        {
            Debug.Log($"[BattleFlow] 场景已有本地玩家 {existing.name}，直接绑定（不重复生成）");
            BindLocalPlayer(existing);
            return;
        }

        var spawnPoint = FindObjectOfType<PlayerSpawnPoint>();
        if (spawnPoint == null)
        {
            Debug.LogWarning("[BattleClient] 场景没有 PlayerSpawnPoint，无法按服务器出生点生成玩家");
            return;
        }
        var player = await spawnPoint.SpawnPlayerAt(_serverSpawn);
        if (player == null) return;

        // 竞态兜底：PlayerSpawnPoint 若已生成过本地玩家（跳过判断未及时生效），
        // 销毁它，确保本进程最终只有一个本地玩家（否则两个角色叠在一起）。
        var newPc = player.GetComponent<PlayerController>();
        foreach (var pc in FindObjectsOfType<PlayerController>())
        {
            if (pc != null && !pc.IsRemote && pc != newPc)
            {
                Debug.Log($"[BattleFlow] 销毁重复本地玩家 {pc.name}（保留服务器出生点生成的 {player.name}）");
                Destroy(pc.gameObject);
            }
        }
        // 若之前绑定的是被销毁的旧玩家，清引用后重新绑定新玩家（否则防重会挡住）
        if (_localPlayer != null && _localPlayer != newPc)
            _localPlayer = null;
        BindLocalPlayer(newPc);
    }

    private void SendEdgeInput(BattleInputFlags flags)
    {
        if (_waitFirstSelfSnap || _inputGateTimer > 0f || !Connected || _localPlayer == null) return;   // 首帧对齐前/入场缓冲期忽略输入
        if (_localPlayer.Input != null && _localPlayer.Input.SprintHeld)
            flags |= BattleInputFlags.Sprint;
        Vector2 world = WorldMove();
        var pos = _localPlayer.transform.position;
        _client.SendInput(world.x, world.y, flags, pos.x, pos.y, pos.z);
    }

    /// <summary>
    /// 把本地摇杆转成"世界方向"再上报。
    /// 为什么：摇杆是相对相机的前后左右，主机用它的相机解释会得到错误方向；
    /// 转成世界方向后，主机 Remote 角色直接用它转向，两端方向一致。
    /// </summary>
    private Vector2 WorldMove()
    {
        if (_localPlayer == null) return Vector2.zero;
        Vector2 input = _localPlayer.MoveValue;
        if (input.magnitude < 0.1f || !CameraManager.HasInstance) return input;
        Vector3 dir = CameraManager.Instance.GetMoveDir(input);
        return new Vector2(dir.x, dir.z);
    }

    // ================= 服务器事件 → 游戏对象 =================

    private void OnJoined(BattleJoinInfo info)
    {
        if (!info.Success)
        {
            Debug.LogError($"[BattleClient] 加入失败：{info.Reason}");
            return;
        }

        Debug.Log($"[BattleFlow] 加入成功：槽位 {info.MySlot}，成员 [{string.Join(",", info.Names)}]");
        _mySlot = info.MySlot;
        _waitFirstSelfSnap = true;   // 等第一帧"自己"快照对齐后再解锁移动（防开局错位）
        _serverSpawn = new Vector3(info.SpawnX, info.SpawnY, info.SpawnZ);   // 服务器权威出生点
        _serverSpawnSet = true;
        ApplyServerSpawn();
        EnsureLocalPlayer();   // 非 host：PlayerSpawnPoint 已跳过，这里按服务器出生点生成
        foreach (var name in info.Names)
            if (name != _myName && !singleProcessDemo)
                SpawnGhost(name).Forget();
    }

    /// <summary>
    /// 出生点对齐：完全用服务器（BattleServer.JoinAck）下发的坐标，客户端不做本地推算。
    /// 本地玩家可能晚于 OnJoined 生成（异步），BindLocalPlayer 时会再调一次。
    /// 单进程演示模式跳过：本地玩家就是房主（槽位 0），不该被搬走。
    /// </summary>
    private void ApplyServerSpawn()
    {
        if (singleProcessDemo || _localPlayer == null || !_serverSpawnSet) return;
        if (_slotApplied && _appliedSlot == _mySlot) return;   // 同槽位已应用过才跳过

        _slotSpawnPos = _serverSpawn;   // 服务器权威出生点
        _localPlayer.transform.position = _slotSpawnPos;
        _appliedSlot = _mySlot;
        _slotApplied = true;
        Debug.Log($"[BattleFlow] 服务器出生点（槽位 {_mySlot}）→ {_slotSpawnPos}");
    }

    private void OnSnapshot(BattleSnapshot snap)
    {
        foreach (var item in snap.Items)
        {
            if (item.Name == _myName)
            {
                if (item.Placeholder)
                    continue;   // 占位快照：Remote 还没生成，本地继续钉出生点，不纠偏不解锁

                if (_waitFirstSelfSnap)
                {
                    // 首帧"自己"的快照：直接瞬移对齐（出生点），解锁移动
                    _waitFirstSelfSnap = false;
                    if (_localPlayer != null)
                    {
                        _localPlayer.transform.position = new Vector3(item.PosX, item.PosY, item.PosZ);
                        _localPlayer.transform.rotation = Quaternion.Euler(0f, item.RotY, 0f);
                    }
                    Debug.Log($"[BattleClient] 首帧快照对齐出生点 → ({item.PosX:F1},{item.PosZ:F1})");
                    _inputGateTimer = spawnInputGate;   // 解锁后进入入场缓冲
                }
                else
                {
                    ReconcileLocal(item);
                    _lastSelfTarget = new Vector3(item.PosX, item.PosY, item.PosZ);
                }
            }
            else if (item.Name == "Boss")
            {
                _bossGhost?.ApplySnapshot(item, snap.Tick);   // Boss 幽灵：位置/动画/HP 全由主机快照驱动
                _bossMaxHp = item.MaxHP;
            }
            else if (_ghosts.TryGetValue(item.Name, out var ghost))
                ghost.ApplySnapshot(item);
        }
    }

    /// <summary>
    /// M11：客户端 Boss 幽灵化——销毁本地 AI/碰撞，保留 Animator，
    /// 挂 BattleGhostInterpolator 按主机快照插值/播动画（不再本地模拟，避免双端 Boss 分叉）。
    /// </summary>
    private void GhostifyBoss(BossBrain boss)
    {
        var go = boss.gameObject;
        go.name = "BossGhost";

        // 销毁前先拷走原 BossBrain 的受击特效（动态 AddComponent 的 BossGhostBrain 没有序列化引用）
        var hitVfx = boss.HitVfxPrefab;

        // 销毁"模拟大脑"：BossBrain/行为树等（客户端不跑 Boss AI，防双端分叉）。
        // 保留"表现身体"：Animator（动画）、Collider/CharacterController（碰撞挡人）、模型。
        foreach (var mb in go.GetComponents<MonoBehaviour>())
        {
            if (mb is Animator || mb is BossGhostBrain) continue;
            Destroy(mb);
        }

        // 受击表现接口：玩家攻击能命中（伤害由主机判定，这里只播表现 + 上报命中）
        var bossGhost = go.AddComponent<BossGhostBrain>();
        bossGhost.hitVfxPrefab = hitVfx;
        bossGhost.OnBossHit = SendBossHit;   // 命中 → 上报主机宽容判定扣真 Boss 血（M11）
        var ghost = go.AddComponent<BattleGhostInterpolator>();
        ghost.SetupAsBoss("BeHit", "Death");   // 受击动画状态名是 BeHit（Hit 是 Trigger，不是状态）
        _bossGhost = ghost;
        Debug.Log("[BattleFlow] 客户端 Boss 幽灵化：按主机快照显示");
    }

    /// <summary>客机命中 Boss 幽灵 → 上报主机（M11 伤害闭环：主机宽容判定后扣真 Boss 血）</summary>
    private void SendBossHit(GameObject attacker, float damage)
    {
        if (!Connected || attacker == null || singleProcessDemo) return;
        var t = attacker.transform;
        var f = t.forward;
        // [BossHit] 客户端发出命中上报：主机应该收到并打出 [Host] 判定日志
        Debug.Log($"[BossHit] [Clnt] 命中Boss上报 damage={damage:F1} pos={t.position:F1}");
        _client.SendBossHit(_myName, t.position.x, t.position.y, t.position.z, f.x, f.z, damage);
    }

    /// <summary>本地预测纠偏：误差小继续让本地跑（手感优先），误差大才被主机拉回（权威兜底）</summary>
    private void ReconcileLocal(BattleSnapshotItem item)
    {
        if (!reconcileLocal || _localPlayer == null) return;

        // 大位移动画期间跳过纠偏：闪避/攻击/受击都是 root motion 瞬时位移，
        // 本地立即执行、主机延迟 0~50ms 才执行，偏差必然短暂超阈值。
        // 这是"延迟"不是"错误"：此时纠偏会把本地动画位移拉回（本地原地、主机突进 → 错位）。
        // 动画播完两端位移一致（同一动画、同一输入/事件），自然对齐。
        if (_localPlayer.Locomotion != null && _localPlayer.Locomotion.CurrentState is DashingState)
            return;
        if (_localPlayer.Action != null &&
            (_localPlayer.Action.CurrentState is ATKingState || _localPlayer.Action.CurrentState is HitState))
            return;

        var target = new Vector3(item.PosX, item.PosY, item.PosZ);
        float dist = Vector3.Distance(_localPlayer.transform.position, target);
        if (dist > reconcileThreshold)
        {
            // 平滑拉回：按 pullSpeed 向权威位置靠拢（偏差大拉得快、小拉得慢），不瞬移 → 无鬼影
            _localPlayer.transform.position = Vector3.MoveTowards(
                _localPlayer.transform.position, target, pullSpeed * Time.deltaTime);
            _localPlayer.transform.rotation = Quaternion.Slerp(
                _localPlayer.transform.rotation, Quaternion.Euler(0f, item.RotY, 0f), 10f * Time.deltaTime);

        }
    }

    private void OnBattleEvent(BattleEventMsg e)
    {
        // 单进程演示：客户端没有独立玩家，伤害事件会作用到"房主本地玩家"上造成血量串台，跳过
        if (singleProcessDemo) return;

        // Boss 事件（主机权威 HP）：更新客户端 Boss 血条 / 胜利结算
        if (e.To == "Boss")
        {
            if (e.Type == BattleEventType.Damage)
            {
                float maxHp = _bossMaxHp > 0f ? _bossMaxHp : 100f;
                // [BossSync] 客户端确认主机广播的 Boss 掉血（血条更新依据）
                Debug.Log($"[BossSync] [Clnt] Boss掉血 {e.V1:F1} → 剩 {e.V2:F1}/{maxHp:F0}");
                EventBus.Emit(GameEvents.HPChanged, new HPData(100, e.V2, maxHp));
                EventBus.Emit(GameEvents.HPTextChanged, new HPData(100, e.V2, maxHp));
            }
            else if (e.Type == BattleEventType.Death)
            {
                EventBus.Emit(GameEvents.EnemyDied);   // 胜利结算（GamePanel 打开 WinView）
            }
            return;
        }

        // 别人的伤害：在对应幽灵头上跳伤害数字（受击动作由快照动画状态表现）
        // —— 修复"主机被打客机看不见"：主机掉血对客机来说只有幽灵动画，没有反馈
        if (e.To != _myName)
        {
            if (e.Type == BattleEventType.Damage && _ghosts.TryGetValue(e.To, out var other))
                EventBus.Emit(GameEvents.HitLanded,
                    new DamageData(e.V1, false, other.transform.position + Vector3.up * 1.8f));
            return;
        }

        // 只处理"打我"的事件；自己的伤害由 ApplyNetworkDamage 应用
        if (_localPlayer == null) return;

        switch (e.Type)
        {
            case BattleEventType.Damage:
                _localPlayer.ApplyNetworkDamage(e.V1, e.V2);   // 以主机的新 HP 为准，绕过本地无敌帧
                break;
            case BattleEventType.Death:
                Debug.Log("[BattleClient] 你被击败了");
                break;
        }
    }

    private void OnPlayerLeft(string name)
    {
        if (_ghosts.TryGetValue(name, out var ghost))
        {
            Destroy(ghost.gameObject);
            _ghosts.Remove(name);
            Debug.Log($"[BattleClient] 幽灵销毁：{name} 已离开");
        }
    }

    // ================= 幽灵生成 =================

    private async UniTask SpawnGhost(string name)
    {
        if (_ghosts.ContainsKey(name)) return;

        var prefab = await ResourceManager.Instance.LoadAsync<GameObject>(playerPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[BattleClient] 找不到角色 prefab：{playerPrefabPath}，无法生成 {name} 的幽灵");
            return;
        }

        // 初始位置放出生点即可：幽灵收到第一帧快照会直接落位（BattleGhostInterpolator 处理），
        // 不需要人为偏移，避免开局从错误位置飞过来。
        var spawn = FindObjectOfType<PlayerSpawnPoint>();
        var go = Instantiate(prefab, spawn != null ? spawn.transform.position : Vector3.zero, Quaternion.identity);
        go.name = $"Ghost_{name}";

        var source = go.GetComponent<PlayerController>();   // 仅用于读取攻击动画配置
        var ghost = go.AddComponent<BattleGhostInterpolator>();
        ghost.Setup(source, name);                          // 内部会销毁模拟组件，幽灵只演画面
        _ghosts[name] = ghost;
        Debug.Log($"[BattleClient] 幽灵生成：{name}");
    }
}
