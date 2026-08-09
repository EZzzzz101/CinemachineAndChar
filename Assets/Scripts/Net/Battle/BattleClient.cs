using System;
using System.Collections.Generic;

/// <summary>
/// 战斗客户端会话 — 连房主（Listen Server）的封装，与 LobbyClient 同套路。
/// 为什么需要它：游戏层不该碰 TcpConnection / 消息编解码，只该订阅事件。
/// 职责：连接 + 加入（BattleJoin）→ 上报输入（BattleInput）→ 收快照/事件/离开通知 → 抛给上层。
/// 纯 C#：可脱离 Unity 独立编译与自测。
/// </summary>
public class BattleClient : IDisposable
{
    private readonly MessageRouter _router = new();
    private TcpConnection _conn;
    private string _name;
    private int _inputSeq;   // 输入序号：TCP 下用于调试；将来换 UDP 时用于乱序检测
    private readonly List<byte> _sendBuf = new();   // 输入上报复用缓冲区（消 GC 分配）

    public bool Connected => _conn != null && _conn.IsConnected;
    public string MyName => _name;

    // ---- 上层（Unity 运行时）订阅的事件 ----
    /// <summary>加入结果：成功/失败 + 成员表 + 我的槽位</summary>
    public event Action<BattleJoinInfo> OnJoined;
    /// <summary>收到主机权威快照（定频）</summary>
    public event Action<BattleSnapshot> OnSnapshot;
    /// <summary>收到一次性战斗事件（伤害/死亡）</summary>
    public event Action<BattleEventMsg> OnBattleEvent;
    /// <summary>房间里有人离开（可能是对方掉线）</summary>
    public event Action<string> OnPlayerLeft;
    /// <summary>与房主断开</summary>
    public event Action OnDisconnected;
    /// <summary>错误提示（连接失败等）</summary>
    public event Action<string> OnError;

    public BattleClient()
    {
        _router.Register(NetMessage.BattleJoinAck, OnJoinAck);
        _router.Register(NetMessage.BattleSnapshot, OnSnapshotMsg);
        _router.Register(NetMessage.BattleEvent, OnEventMsg);
        _router.Register(NetMessage.BattleLeaveNotify, OnLeaveMsg);
    }

    /// <summary>连接房主并请求加入（名字即"我是谁"，服务端据此建成员表）</summary>
    public void Connect(string ip, int port, string name)
    {
        Disconnect();   // 防御：重复连接（重试/误调）先断开旧的，避免幽灵连接留在服务器
        _name = name;
        _conn = new TcpConnection();
        _conn.OnMessage += payload => _router.Dispatch(_conn, payload);
        _conn.OnDisconnected += () => OnDisconnected?.Invoke();

        if (!_conn.Connect(ip, port))
        {
            OnError?.Invoke($"连接房主失败 {ip}:{port}");
            return;
        }
        Send(NetMessage.BattleJoin, MsgBattleJoin.Encode(name));
    }

    /// <summary>主线程每帧调用：把收包队列分发成事件（与 LobbyClient 一样）</summary>
    public void Poll() => _conn?.Poll();

    /// <summary>
    /// 上报输入：moveX/moveZ 是摇杆连续值，flags 是边沿（闪避/攻击按下）。
    /// 调用时机由上层定：边沿事件即时发、移动状态定频发。
    /// </summary>
    public void SendInput(float moveX, float moveZ, BattleInputFlags flags)
        => SendInput(moveX, moveZ, flags, 0f, 0f, 0f);

    /// <summary>上报输入（带客户端本地位置，供主机生成 Remote 时对齐起点）</summary>
    public void SendInput(float moveX, float moveZ, BattleInputFlags flags,
                          float posX, float posY, float posZ)
    {
        if (!Connected) return;
        _sendBuf.Clear();
        MsgBattleInput.EncodeInto(_sendBuf, ++_inputSeq, moveX, moveZ, flags, posX, posY, posZ);
        _conn.Send(_sendBuf);
    }

    /// <summary>上报"客机命中 Boss"：主机据此宽容判定后扣真 Boss 血（M11 伤害闭环）</summary>
    public void SendBossHit(string attacker, float posX, float posY, float posZ,
                            float fwdX, float fwdZ, float damage)
    {
        if (!Connected) return;
        Send(NetMessage.BattleBossHit, MsgBossHit.Encode(attacker, posX, posY, posZ, fwdX, fwdZ, damage));
    }

    public void Disconnect() => _conn?.Disconnect();

    private void Send(int msgId, byte[] body)
    {
        if (_conn == null || !_conn.IsConnected)
        {
            OnError?.Invoke("未连接房主");
            return;
        }
        _conn.Send(NetMessage.Encode(msgId, body));
    }

    // ============ 房主回执 ============

    private void OnJoinAck(TcpConnection conn, byte[] body)
    {
        var ack = MsgBattleJoinAck.Decode(body);
        OnJoined?.Invoke(new BattleJoinInfo
        {
            Success = ack.Success,
            Reason = ack.Reason,
            MySlot = ack.MySlot,
            Names = ack.Names,
            SpawnX = ack.SpawnX,
            SpawnY = ack.SpawnY,
            SpawnZ = ack.SpawnZ,
        });
    }

    private void OnSnapshotMsg(TcpConnection conn, byte[] body)
    {
        var msg = MsgBattleSnapshot.Decode(body);
        OnSnapshot?.Invoke(new BattleSnapshot { Tick = msg.Tick, Items = msg.Items });
    }

    private void OnEventMsg(TcpConnection conn, byte[] body)
    {
        var msg = MsgBattleEvent.Decode(body);
        OnBattleEvent?.Invoke(new BattleEventMsg
        {
            Type = msg.Type,
            From = msg.From,
            To = msg.To,
            V1 = msg.V1,
            V2 = msg.V2,
        });
    }

    private void OnLeaveMsg(TcpConnection conn, byte[] body)
    {
        OnPlayerLeft?.Invoke(MsgBattleLeaveNotify.Decode(body).Name);
    }

    public void Dispose() => Disconnect();
}
