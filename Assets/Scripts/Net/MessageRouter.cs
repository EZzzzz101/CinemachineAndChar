using System;
using System.Collections.Generic;

/// <summary>
/// 消息路由器 — 按 msgId 分发消息，替代 if-else 链。
/// 用法：启动时把每个 msgId 的处理函数登记进表；
/// 收到 payload 后调 Dispatch，它自动解出 msgId 并调用对应处理函数。
/// 客户端、服务器通用。
/// </summary>
public class MessageRouter
{
    private readonly Dictionary<int, Action<TcpConnection, byte[]>> _handlers = new();

    /// <summary>登记：msgId → 处理函数（参数：谁发来的连接 + 解好的 body）</summary>
    public void Register(int msgId, Action<TcpConnection, byte[]> handler)
    {
        _handlers[msgId] = handler;
    }

    /// <summary>收到一条 payload 后调用：解出 msgId → 找处理函数 → 执行</summary>
    public bool Dispatch(TcpConnection conn, byte[] payload)
    {
        if (!NetMessage.TryDecode(payload, out int msgId, out byte[] body))
        {
            NetLog.Warn("[MessageRouter] 消息解析失败（payload 太短）");
            return false;
        }

        if (_handlers.TryGetValue(msgId, out var handler))
        {
            handler(conn, body);
            return true;
        }

        NetLog.Warn($"[MessageRouter] 未注册的 msgId：{msgId}");
        return false;
    }

    public void Clear() => _handlers.Clear();
}
