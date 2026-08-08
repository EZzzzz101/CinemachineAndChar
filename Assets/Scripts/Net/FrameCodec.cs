using System;

/// <summary>
/// 帧编解码 — 解决 TCP 的"半包/粘包"问题。
/// 约定：每条消息 = [4字节长度(大端)] + [消息体]。
/// </summary>
public static class FrameCodec
{
    public const int HeaderSize = 4;
    public const int MaxFrameSize = 1 << 20;   // 单帧上限 1MB，防止恶意长度打爆内存

    /// <summary>把消息体包装成帧：[长度][消息体]</summary>
    public static byte[] Encode(byte[] payload)
    {
        if (payload == null) throw new ArgumentNullException(nameof(payload));
        if (payload.Length > MaxFrameSize)
            throw new ArgumentException($"消息体超过上限：{payload.Length}");

        var frame = new byte[HeaderSize + payload.Length];
        int len = payload.Length;

        // 大端（网络字节序）：高字节在前
        frame[0] = (byte)(len >> 24);
        frame[1] = (byte)(len >> 16);
        frame[2] = (byte)(len >> 8);
        frame[3] = (byte)len;

        Buffer.BlockCopy(payload, 0, frame, HeaderSize, payload.Length);
        return frame;
    }

    /// <summary>
    /// 从接收缓冲区解析一帧。
    /// 返回 true 表示拿到完整消息；false 表示数据还不够（半包），等下一次 Read 再试。
    /// consumed 表示这一帧占了多少字节（用于从缓冲区移除，支持粘包循环）。
    /// </summary>
    public static bool TryExtract(byte[] buffer, int count, out byte[] payload, out int consumed)
    {
        payload = null;
        consumed = 0;

        if (count < HeaderSize) return false;   // 连长度都还没收全

        int len = (buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3];
        if (len <= 0 || len > MaxFrameSize)
            throw new InvalidOperationException($"非法帧长度：{len}");

        if (count < HeaderSize + len) return false;   // 长度有了，但消息体还没到齐（半包）

        payload = new byte[len];
        Buffer.BlockCopy(buffer, HeaderSize, payload, 0, len);
        consumed = HeaderSize + len;
        return true;
    }
}