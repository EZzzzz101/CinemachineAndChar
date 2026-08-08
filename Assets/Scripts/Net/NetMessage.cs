using System;

/// <summary>
/// 消息协议 — 在传输层之上定义"消息"：
/// 消息体(payload) = [4字节 msgId][body]。
/// msgId 放头部的原因：收端必须先知道类型，才知道该反序列化成哪个类。
/// </summary>
public static class NetMessage
{
    // ---- 消息 ID 表（前后端共用；编号是协议契约，别删中间的，只往后加） ----
    public const int Register     = 1;   // 客户端→服务器：注册用户名
    public const int RegisterAck  = 2;   // 服务器→客户端：注册结果（成功/失败+原因）
    public const int Search       = 3;   // 客户端→服务器：搜索玩家
    public const int SearchAck    = 4;   // 服务器→客户端：搜索结果
    public const int Invite       = 5;   // 客户端→服务器：请求邀请某玩家
    public const int InviteNotify = 6;   // 服务器→被邀请者：有人邀请你
    public const int InviteAck    = 7;   // 被邀请者→服务器：接受/拒绝
    public const int JoinRoom     = 8;   // 服务器→双方：进房通知（带房主地址）
    public const int InviteResult = 9;   // 服务器→邀请人：邀请结果（接受/拒绝/不在线）
    // ...后续战斗同步消息继续往后加

    public const int MsgIdSize = 4;

    /// <summary>把 [msgId + body] 拼成一条消息体（payload），交给 FrameCodec 再封帧</summary>
    public static byte[] Encode(int msgId, byte[] body)
    {
        var payload = new byte[MsgIdSize + (body?.Length ?? 0)];
        payload[0] = (byte)(msgId >> 24);
        payload[1] = (byte)(msgId >> 16);
        payload[2] = (byte)(msgId >> 8);
        payload[3] = (byte)msgId;
        if (body != null && body.Length > 0)
            Buffer.BlockCopy(body, 0, payload, MsgIdSize, body.Length);
        return payload;
    }

    /// <summary>从一条消息体里解出 msgId 和 body（FrameCodec 拆帧之后调用）</summary>
    public static bool TryDecode(byte[] payload, out int msgId, out byte[] body)
    {
        msgId = 0;
        body = null;
        if (payload == null || payload.Length < MsgIdSize) return false;

        msgId = (payload[0] << 24) | (payload[1] << 16) | (payload[2] << 8) | payload[3];
        int bodyLen = payload.Length - MsgIdSize;
        body = new byte[bodyLen];
        Buffer.BlockCopy(payload, MsgIdSize, body, 0, bodyLen);
        return true;
    }
}
