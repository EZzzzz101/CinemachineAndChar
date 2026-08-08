using System;
using UnityEngine;

/// <summary>
/// 大厅流程自测：同一进程模拟两个客户端 A、B 连本地大厅服务器。
/// 连接带重试：服务器没起来就每 0.5 秒再试，直到连上。
/// </summary>
public class LobbyClientSim : MonoBehaviour
{
    [Header("客户端模拟")]
    [SerializeField] private int lobbyPort = 7777;
    [SerializeField] private float retryInterval = 0.5f;

    private TcpConnection _clientA;
    private TcpConnection _clientB;
    private float _retryTimer;

    private void Update()
    {
        // 重试：没连上就定时再试（服务器可能还没起来）
        _retryTimer -= Time.deltaTime;
        if (_retryTimer <= 0f)
        {
            _retryTimer = retryInterval;
            if (_clientA == null) _clientA = TryConnect("A", OnAMessage);
            if (_clientB == null) _clientB = TryConnect("B", OnBMessage);
        }

        _clientA?.Poll();
        _clientB?.Poll();
    }

    /// <summary>尝试连接；成功返回连接并立刻发注册，失败返回 null 等下次重试</summary>
    private TcpConnection TryConnect(string name, Action<byte[]> onMessage)
    {
        var conn = new TcpConnection();
        conn.OnMessage += onMessage;
        if (!conn.Connect("127.0.0.1", lobbyPort))
        {
            conn.Disconnect();   // 失败要清理，否则留半截对象
            return null;
        }

        conn.Send(NetMessage.Encode(NetMessage.Register, MsgRegister.Encode(name)));
        Debug.Log($"[Sim][{name}] 已连接并注册");
        return conn;
    }

    // ---- A 的流程：注册ok → 搜索B → 邀请B ----
    private void OnAMessage(byte[] payload)
    {
        if (!NetMessage.TryDecode(payload, out int msgId, out byte[] body)) return;

        switch (msgId)
        {
            case NetMessage.RegisterAck:
                var ack = MsgRegisterAck.Decode(body);
                Debug.Log($"[Sim][A] 注册结果：{ack.Reason}");
                if (ack.Success)
                    _clientA.Send(NetMessage.Encode(NetMessage.Search, MsgSearch.Encode("B")));
                break;

            case NetMessage.SearchAck:
                var sa = MsgSearchAck.Decode(body);
                Debug.Log($"[Sim][A] 搜索B：{(sa.Found ? "在线" : "不在线")}");
                if (sa.Found)
                    _clientA.Send(NetMessage.Encode(NetMessage.Invite, MsgInvite.Encode("B")));
                break;

            case NetMessage.JoinRoom:
                var room = MsgJoinRoom.Decode(body);
                Debug.Log($"[Sim][A] 我是房主，房间 {room.RoomId} 已建，战斗地址 {room.HostIp}:{room.HostPort}");
                break;
        }
    }

    // ---- B 的流程：注册ok → 收到邀请 → 接受 ----
    private void OnBMessage(byte[] payload)
    {
        if (!NetMessage.TryDecode(payload, out int msgId, out byte[] body)) return;

        switch (msgId)
        {
            case NetMessage.RegisterAck:
                var ack = MsgRegisterAck.Decode(body);
                Debug.Log($"[Sim][B] 注册结果：{ack.Reason}");
                break;

            case NetMessage.InviteNotify:
                var inv = MsgInviteNotify.Decode(body);
                Debug.Log($"[Sim][B] 收到 {inv.InviterName} 的邀请，接受");
                _clientB.Send(NetMessage.Encode(NetMessage.InviteAck, MsgInviteAck.Encode(true)));
                break;

            case NetMessage.JoinRoom:
                var room = MsgJoinRoom.Decode(body);
                Debug.Log($"[Sim][B] 被拉进房间 {room.RoomId}，将连接 {room.HostIp}:{room.HostPort} 战斗");
                break;
        }
    }

    private void OnDestroy()
    {
        _clientA?.Disconnect();
        _clientB?.Disconnect();
    }
}
