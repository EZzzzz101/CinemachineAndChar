using System;
using System.Collections.Generic;

/// <summary>
/// 战斗服务器（主机权威）— 房主进程内的"房间裁判"。
///
/// 为什么需要它：TcpServer 只会收发字节，不知道"这条连接是谁、收到输入该干嘛"。
/// BattleServer 把战斗房间的职责收拢成一类：
///   成员表（谁在房间）→ 输入转发（输入喂给谁）→ 快照广播（权威状态发给谁）→ 事件广播（伤害/死亡通知谁）。
///
/// 为什么是纯 C#：
///   1) 位置/动画在 Unity 对象上，所以快照数据由 Unity 运行时每 50ms 采集成纯数据列表传进来（Tick），
///      这里只负责封帧 + 群发，不碰 Transform —— 网络逻辑可脱离引擎独立自测；
///   2) 输入用事件 OnInput 抛出去，由 Unity 运行时决定喂给哪个角色的 RemoteInputProvider —— 网络层与游戏层解耦。
/// </summary>
public class BattleServer : IDisposable
{
    private readonly TcpServer _server = new();
    private readonly MessageRouter _router = new();

    // 成员表：名字 → 连接。
    // 为什么用名字做 key：BattleInput 消息里不带名字（名字只在加入时声明一次），
    // 服务器靠"哪条连接发来的"反查名字，再决定把输入喂给谁。
    private readonly Dictionary<string, TcpConnection> _members = new();
    private readonly Dictionary<TcpConnection, string> _connNames = new();

    private string _hostName = "Host";   // 房主本地玩家：不占连接，固定槽位 0
    private int _nextSlot = 1;           // 网络玩家槽位从 1 开始递增
    private readonly List<string> _preRegistered = new();   // 大厅预登记成员（含房主），顺序即槽位
    private readonly List<float[]> _spawnPoints = new();    // 槽位 → 出生点 [x,y,z]（主机权威，JoinAck 下发）
    private readonly List<byte> _snapBuf = new();           // 快照广播复用缓冲区（消 GC 分配）

    /// <summary>收到客户端输入：参数 = 玩家名 + 输入状态（Unity 运行时接住，喂给对应 RemoteInputProvider）</summary>
    public event Action<string, BattleInputState> OnInput;
    /// <summary>收到客机命中 Boss 上报：参数 = 玩家名 + 命中信息（Unity 运行时宽容判定后扣真 Boss 血）</summary>
    public event Action<string, MsgBossHit> OnBossHit;
    /// <summary>新玩家加入（可用于日志 / UI）</summary>
    public event Action<string> OnPlayerJoined;
    /// <summary>玩家离开 / 断线（Unity 运行时据此销毁对应的远端角色）</summary>
    public event Action<string> OnPlayerLeft;

    public bool Start(int port)
    {
        _router.Register(NetMessage.BattleJoin, OnBattleJoin);
        _router.Register(NetMessage.BattleInput, OnBattleInput);
        _router.Register(NetMessage.BattleBossHit, OnBossHitMsg);

        _server.OnClientConnected += OnClientConnected;
        return _server.Start(port);
    }

    /// <summary>设置房主本地玩家名字（进战斗前由 Unity 运行时调用；房间"槽位 0"永远是房主）</summary>
    public void SetHostName(string name) => _hostName = name;

    /// <summary>
    /// 预登记成员表（来自大厅 JoinRoom：房主 + 客人，顺序即槽位）。
    /// 客人连上来时按名单分配槽位，名单外的人拒绝（防外人混入）。
    /// 手动调试（无大厅）时不调用，槽位按加入顺序兜底。
    /// </summary>
    public void PreRegister(List<string> names)
    {
        _preRegistered.Clear();
        if (names != null) _preRegistered.AddRange(names);
    }

    /// <summary>
    /// 设置槽位出生点列表（房主运行时从场景读基准生成，索引 = 槽位）。
    /// 客户端不再自己推算出生点，直接用这里下发的坐标。
    /// </summary>
    public void SetSpawnPoints(List<float[]> points)
    {
        _spawnPoints.Clear();
        if (points != null) _spawnPoints.AddRange(points);
    }

    /// <summary>主线程每帧调用：驱动 Accept + 消息分发（和 LobbyServer 一样）</summary>
    public void Poll() => _server.Poll();

    // ================= 连接生命周期 =================

    private void OnClientConnected(TcpConnection conn)
    {
        conn.OnMessage += payload => _router.Dispatch(conn, payload);
        conn.OnDisconnected += () => OnDisconnected(conn);
    }

    private void OnDisconnected(TcpConnection conn)
    {
        if (!_connNames.TryGetValue(conn, out var name)) return;

        _connNames.Remove(conn);
        _members.Remove(name);
        OnPlayerLeft?.Invoke(name);
        NetLog.Log($"[Battle] {name} 离开战斗，当前 {_members.Count + 1} 人（含房主）");

        // 通知剩下的人：广播离开消息（谁掉线了，客户端好销毁对应幽灵角色）
        var payload = NetMessage.Encode(NetMessage.BattleLeaveNotify, MsgBattleLeaveNotify.Encode(name));
        foreach (var other in _members.Values)
            other.Send(payload);
    }

    // ================= 消息处理 =================

    private void OnBattleJoin(TcpConnection conn, byte[] body)
    {
        var name = MsgBattleJoin.Decode(body).Name;

        // 防呆：空名字 / 与房主或其他玩家重名 → 拒绝。名字是"连接 ↔ 玩家"的唯一凭证。
        if (string.IsNullOrEmpty(name))
        {
            SendJoinAck(conn, false, "名字不能为空", -1);
            return;
        }
        if (name == _hostName || _members.ContainsKey(name))
        {
            SendJoinAck(conn, false, "名字已被占用", -1);
            return;
        }

        // 槽位：优先用大厅预登记顺序（房主 0、客人 1...）
        int slot = _preRegistered.Count > 0 ? _preRegistered.IndexOf(name) : -1;
        if (slot < 0)
        {
            if (_preRegistered.Count > 0)
            {
                // 有预登记表但名单里没有他 → 不是本房间成员，拒绝
                SendJoinAck(conn, false, "你不是本房间成员", -1);
                return;
            }
            slot = _nextSlot++;   // 手动调试兜底：按加入顺序
        }

        _members[name] = conn;
        _connNames[conn] = name;
        SendJoinAck(conn, true, "ok", slot);
        OnPlayerJoined?.Invoke(name);
        NetLog.Log($"[Battle] {name} 加入战斗，当前 {_members.Count + 1} 人（含房主）");
    }

    /// <summary>回执里带成员表：客户端因此不依赖大厅状态，只认战斗服务器的名单（解耦）</summary>
    private void SendJoinAck(TcpConnection conn, bool ok, string reason, int mySlot)
    {
        // 服务器权威出生点：按槽位查，缺省 0
        float sx = 0f, sy = 0f, sz = 0f;
        if (mySlot >= 0 && mySlot < _spawnPoints.Count)
        {
            var p = _spawnPoints[mySlot];
            sx = p[0]; sy = p[1]; sz = p[2];
        }
        conn.Send(NetMessage.Encode(
            NetMessage.BattleJoinAck,
            MsgBattleJoinAck.Encode(ok, reason, mySlot, RosterNames(), sx, sy, sz)));
    }

    private List<string> RosterNames()
    {
        var names = new List<string> { _hostName };
        foreach (var kv in _members)
            names.Add(kv.Key);
        return names;
    }

    private void OnBattleInput(TcpConnection conn, byte[] body)
    {
        // 未加入就发输入：直接忽略（防垃圾消息/未握手连接）
        if (!_connNames.TryGetValue(conn, out var name)) return;

        var msg = MsgBattleInput.Decode(body);
        OnInput?.Invoke(name, new BattleInputState
        {
            MoveX = msg.MoveX,
            MoveZ = msg.MoveZ,
            Flags = msg.Flags,
            PosX = msg.PosX,
            PosY = msg.PosY,
            PosZ = msg.PosZ,
        });
    }

    private void OnBossHitMsg(TcpConnection conn, byte[] body)
    {
        // 未加入就发命中：忽略
        if (!_connNames.TryGetValue(conn, out var name)) return;
        OnBossHit?.Invoke(name, MsgBossHit.Decode(body));
    }

    // ================= 主机权威广播 =================

    /// <summary>
    /// 定频广播快照（Unity 运行时每 50ms 调用一次，tick 递增）：
    /// 位置/动画在 Unity 对象上，运行时负责采集成纯数据列表传进来，这里只封帧 + 群发。
    /// </summary>
    public void Tick(int tick, List<BattleSnapshotItem> items)
    {
        if (_members.Count == 0) return;   // 没人观战就不浪费带宽

        _snapBuf.Clear();
        MsgBattleSnapshot.EncodeInto(_snapBuf, tick, items);
        foreach (var conn in _members.Values)
            conn.Send(_snapBuf);
    }

    /// <summary>
    /// 广播一次性事件（伤害/死亡）：例如主机上 Boss 打中某玩家时由运行时调用。
    /// 快照负责"持续状态"，事件负责"瞬间反馈"，两者分工不同。
    /// </summary>
    public void BroadcastEvent(BattleEventType type, string from, string to, float v1, float v2)
    {
        var payload = NetMessage.Encode(NetMessage.BattleEvent, MsgBattleEvent.Encode(type, from, to, v1, v2));
        foreach (var conn in _members.Values)
            conn.Send(payload);
    }

    public void Stop()
    {
        _server.Stop();
        _members.Clear();
        _connNames.Clear();
        _router.Clear();
    }

    public void Dispose() => Stop();
}
