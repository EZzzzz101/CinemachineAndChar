using System;
using System.Collections.Generic;

/// <summary>
/// 大厅服务器（M7a）— 联机的"大脑"。
/// 职责：接客(TcpServer) + 分发(MessageRouter) + 在线表 + 邀请转发 + 房间分配。
/// 核心思想：所有客户端都连着服务器，所以服务器能"替房主把邀请发出去"。
/// 使用：外部每帧调 Poll()；Start 开始监听；Stop 关服。
/// </summary>
public class LobbyServer : IDisposable
{
    private readonly TcpServer _server = new();
    private readonly MessageRouter _router = new();

    // 在线表：用户名 → 连接（这就是"数据库"，内存版，主机权威）
    private readonly Dictionary<string, TcpConnection> _online = new();

    // 待确认的邀请：被邀请者连接 → 邀请人连接（B 接受时，服务器才知道通知谁）
    private readonly Dictionary<TcpConnection, TcpConnection> _pendingInvites = new();

    // 房间表：房间号 → 成员
    private readonly Dictionary<int, List<TcpConnection>> _rooms = new();
    private int _nextRoomId = 1;

    public const int BattlePort = 7778;   // 房主战斗监听端口（约定，第 7 课用）

    public event Action<string> OnPlayerJoined;   // 有人上线（可接大厅列表 UI）
    public event Action<string> OnPlayerLeft;     // 有人离线

    public int OnlineCount => _online.Count;

    public bool Start(int port)
    {
        _router.Register(NetMessage.Register, OnRegister);
        _router.Register(NetMessage.Search, OnSearch);
        _router.Register(NetMessage.Invite, OnInvite);
        _router.Register(NetMessage.InviteAck, OnInviteAck);
        _router.Register(NetMessage.RoomStart, OnRoomStart);

        _server.OnClientConnected += OnClientConnected;
        return _server.Start(port);
    }

    public void Poll() => _server.Poll();

    private void OnClientConnected(TcpConnection conn)
    {
        // 每个连接统一走路由；断线时清理在线表
        conn.OnMessage += payload => _router.Dispatch(conn, payload);
        conn.OnDisconnected += () => OnDisconnected(conn);
    }

    // ================= 消息处理 =================

    private void OnRegister(TcpConnection conn, byte[] body)
    {
        var name = MsgRegister.Decode(body).UserName;

        if (string.IsNullOrEmpty(name))
        {
            conn.Send(NetMessage.Encode(NetMessage.RegisterAck, MsgRegisterAck.Encode(false, "名字不能为空")));
            return;
        }
        if (_online.ContainsKey(name))
        {
            conn.Send(NetMessage.Encode(NetMessage.RegisterAck, MsgRegisterAck.Encode(false, "名字已被占用")));
            return;
        }

        _online[name] = conn;
        conn.Send(NetMessage.Encode(NetMessage.RegisterAck, MsgRegisterAck.Encode(true, "ok")));
        OnPlayerJoined?.Invoke(name);
        NetLog.Log($"[Lobby] {name} 上线，当前 {_online.Count} 人在线");
    }

    private void OnSearch(TcpConnection conn, byte[] body)
    {
        var keyword = MsgSearch.Decode(body).Keyword;
        bool found = _online.TryGetValue(keyword, out var target) && target != conn;
        conn.Send(NetMessage.Encode(NetMessage.SearchAck, MsgSearchAck.Encode(found, keyword)));
    }

    private void OnInvite(TcpConnection conn, byte[] body)
    {
        var targetName = MsgInvite.Decode(body).TargetName;

        if (!_online.TryGetValue(targetName, out var target))
        {
            NetLog.Warn($"[Lobby] 邀请失败：{targetName} 不在线");
            conn.Send(NetMessage.Encode(NetMessage.InviteResult, MsgInviteResult.Encode(false, $"{targetName} 不在线")));
            return;
        }

        // 记录"谁邀请了谁"，等被邀请者回 InviteAck 时用
        _pendingInvites[target] = conn;

        // 服务器替房主把邀请转发给被邀请者（这就是"主动邀请"的实现）
        target.Send(NetMessage.Encode(NetMessage.InviteNotify, MsgInviteNotify.Encode(GetName(conn))));
        NetLog.Log($"[Lobby] {GetName(conn)} 邀请 {targetName}");
    }

    private void OnInviteAck(TcpConnection conn, byte[] body)
    {
        var accept = MsgInviteAck.Decode(body).Accept;

        // conn 是被邀请者；查他对应的邀请人
        if (!_pendingInvites.TryGetValue(conn, out var inviter))
        {
            NetLog.Warn("[Lobby] 收到未知邀请回执");
            return;
        }
        _pendingInvites.Remove(conn);

        if (!accept)
        {
            NetLog.Log($"[Lobby] {GetName(conn)} 拒绝了邀请");
            inviter.Send(NetMessage.Encode(NetMessage.InviteResult, MsgInviteResult.Encode(false, $"{GetName(conn)} 拒绝了邀请")));
            return;
        }

        // 接受：建房间，把房主地址（邀请人的 IP + 战斗端口）发给双方
        int roomId = _nextRoomId++;
        _rooms[roomId] = new List<TcpConnection> { inviter, conn };

        var hostIp = inviter.RemoteIp;
        inviter.Send(NetMessage.Encode(NetMessage.InviteResult, MsgInviteResult.Encode(true, $"{GetName(conn)} 已接受")));
        inviter.Send(NetMessage.Encode(NetMessage.JoinRoom, MsgJoinRoom.Encode(GetName(inviter), GetName(conn), hostIp, BattlePort, roomId)));
        conn.Send(NetMessage.Encode(NetMessage.JoinRoom, MsgJoinRoom.Encode(GetName(inviter), GetName(conn), hostIp, BattlePort, roomId)));
        NetLog.Log($"[Lobby] 房间 {roomId} 创建：{GetName(inviter)}(房主) + {GetName(conn)}");
    }

    /// <summary>
    /// 开始战斗：房主（或其他成员）点"进入游戏" → 大厅把开始信号转发给同房间的其他成员，
    /// 大家同时进战斗场景（房主自己由客户端直接加载）。
    /// </summary>
    private void OnRoomStart(TcpConnection conn, byte[] body)
    {
        foreach (var kv in _rooms)
        {
            if (!kv.Value.Contains(conn)) continue;

            var payload = NetMessage.Encode(NetMessage.RoomStart, MsgRoomStart.Encode(kv.Key));
            foreach (var member in kv.Value)
            {
                if (member != conn)   // 转发给其他人；发起者自己本地加载
                    member.Send(payload);
            }
            NetLog.Log($"[Lobby] 房间 {kv.Key} 开始战斗，通知其他成员进场景");
            return;
        }
        NetLog.Warn("[Lobby] 收到开始战斗请求但连接不在任何房间");
    }

    // ================= 断线清理 =================

    private void OnDisconnected(TcpConnection conn)
    {
        var name = GetName(conn);
        if (name != "?")
        {
            _online.Remove(name);
            OnPlayerLeft?.Invoke(name);
            NetLog.Log($"[Lobby] {name} 离线，当前 {_online.Count} 人在线");
        }
        _pendingInvites.Remove(conn);
    }

    private string GetName(TcpConnection conn)
    {
        foreach (var kv in _online)
            if (kv.Value == conn) return kv.Key;
        return "?";
    }

    public void Stop()
    {
        _server.Stop();
        _online.Clear();
        _pendingInvites.Clear();
        _rooms.Clear();
        _router.Clear();
    }

    public void Dispose() => Stop();
}
