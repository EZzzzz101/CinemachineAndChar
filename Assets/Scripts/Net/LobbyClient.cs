using System;

/// <summary>
/// 大厅客户端会话层（M7b）— 封装"连大厅 + 收发消息 + 状态"。
/// UI 只订阅事件、调方法，不直接碰 TcpConnection / NetMessage。
/// </summary>
public class LobbyClient
{
    private readonly MessageRouter _router = new();
    private TcpConnection _conn;
    private string _name;

    public bool Connected => _conn != null && _conn.IsConnected;
    public bool Registered { get; private set; }
    public string MyName => _name;

    // ---- UI 订阅的事件 ----
    public event Action<string> OnRegisterResult;        // 参数：结果说明（成功时 Registered=true）
    public event Action<bool, string> OnSearchResult;    // 参数：是否在线、名字
    public event Action<string> OnInvited;               // 参数：邀请人名字
    public event Action<string, string, string, int, int> OnJoinedRoom;  // 房主名、客人名、房主IP、端口、房间号
    public event Action<bool, string> OnInviteResult;    // 参数：是否接受、原因（我发出邀请后的反馈）
    public event Action<string> OnError;                 // 参数：错误信息
    public event Action OnDisconnected;

    public LobbyClient()
    {
        _router.Register(NetMessage.RegisterAck, OnRegisterAck);
        _router.Register(NetMessage.SearchAck, OnSearchAck);
        _router.Register(NetMessage.InviteNotify, OnInviteNotify);
        _router.Register(NetMessage.JoinRoom, OnJoinRoom);
        _router.Register(NetMessage.InviteResult, HandleInviteResult);
    }

    /// <summary>连接大厅并注册用户名</summary>
    public void Connect(string ip, int port, string userName)
    {
        _name = userName;
        _conn = new TcpConnection();
        _conn.OnMessage += payload => _router.Dispatch(_conn, payload);
        _conn.OnDisconnected += () => OnDisconnected?.Invoke();

        if (!_conn.Connect(ip, port))
        {
            OnError?.Invoke($"连接失败 {ip}:{port}（服务器没开？IP 对不对？）");
            return;
        }
        Send(NetMessage.Register, MsgRegister.Encode(userName));
    }

    public void Poll() => _conn?.Poll();

    public void Search(string keyword) => Send(NetMessage.Search, MsgSearch.Encode(keyword));
    public void Invite(string targetName) => Send(NetMessage.Invite, MsgInvite.Encode(targetName));
    public void ReplyInvite(bool accept) => Send(NetMessage.InviteAck, MsgInviteAck.Encode(accept));

    public void Disconnect() => _conn?.Disconnect();

    private void Send(int msgId, byte[] body)
    {
        if (_conn == null || !_conn.IsConnected)
        {
            OnError?.Invoke("未连接服务器");
            return;
        }
        _conn.Send(NetMessage.Encode(msgId, body));
    }

    // ============ 服务器回执 ============

    private void OnRegisterAck(TcpConnection conn, byte[] body)
    {
        var ack = MsgRegisterAck.Decode(body);
        Registered = ack.Success;
        OnRegisterResult?.Invoke(ack.Reason);
    }

    private void OnSearchAck(TcpConnection conn, byte[] body)
    {
        var ack = MsgSearchAck.Decode(body);
        OnSearchResult?.Invoke(ack.Found, ack.Name);
    }

    private void OnInviteNotify(TcpConnection conn, byte[] body)
    {
        var msg = MsgInviteNotify.Decode(body);
        OnInvited?.Invoke(msg.InviterName);
    }

    private void OnJoinRoom(TcpConnection conn, byte[] body)
    {
        var msg = MsgJoinRoom.Decode(body);
        OnJoinedRoom?.Invoke(msg.HostName, msg.GuestName, msg.HostIp, msg.HostPort, msg.RoomId);
    }

    private void HandleInviteResult(TcpConnection conn, byte[] body)
    {
        var msg = MsgInviteResult.Decode(body);
        OnInviteResult?.Invoke(msg.Accepted, msg.Reason);
    }
}
