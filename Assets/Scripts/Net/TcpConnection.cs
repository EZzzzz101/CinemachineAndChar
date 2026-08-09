using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

/// <summary>
/// TCP 连接 — 一条已经建立的双向通道。
/// 线程模型：后台收包线程读数据拼帧入队；主线程每帧 Poll() 取出并触发事件。
/// 为什么这么绕：Unity API 只能在主线程调用，所以"收到消息"必须发生在主线程。
/// </summary>
public class TcpConnection : IDisposable
{
    private readonly object _lock = new();
    private readonly Queue<byte[]> _inbox = new();
    private readonly List<byte> _recvBuffer = new();

    private TcpClient _client;
    private NetworkStream _stream;
    private Thread _recvThread;
    private volatile bool _running;
    private bool _disconnected;
    private bool _disconnectEventFired;
    private byte[] _sendFrame = new byte[256];   // 发送帧复用缓冲区（避免每帧 new 数组 → GC 压力）

    /// <summary>收到一条完整消息（主线程 Poll 时触发）</summary>
    public event Action<byte[]> OnMessage;

    /// <summary>连接断开（主线程 Poll 时触发，只触发一次）</summary>
    public event Action OnDisconnected;

    public bool IsConnected => _client != null && _client.Connected && !_disconnected;

    /// <summary>对端 IP（服务器用它把房主地址告诉被邀请者）</summary>
    public string RemoteIp
    {
        get
        {
            try { return ((IPEndPoint)_client.Client.RemoteEndPoint).Address.ToString(); }
            catch { return "127.0.0.1"; }
        }
    }
    /// <summary>客户端模式：主动连接服务器</summary>
    public bool Connect(string ip, int port)
    {
        try
        {
            _client = new TcpClient();
            _client.Connect(ip, port);
            BeginReceive();
            return true;
        }
        catch (Exception e)
        {
            NetLog.Error($"[TcpConnection] 连接失败 {ip}:{port}：{e.Message}");
            _disconnected = true;
            return false;
        }
    }

    /// <summary>服务器模式：TcpServer Accept 到新连接后，把已连接的 TcpClient 交进来</summary>
    internal void Attach(TcpClient client)
    {
        _client = client;
        BeginReceive();
    }

    private void BeginReceive()
    {
        _stream = _client.GetStream();
        _running = true;
        _recvThread = new Thread(ReceiveLoop) { IsBackground = true };
        _recvThread.Start();
    }

    /// <summary>发送消息（主线程调用）</summary>
    public void Send(byte[] payload)
    {
        if (!IsConnected) return;
        try
        {
            // 复用帧缓冲区：长度头 + payload 写进 _sendFrame，不再每次 new byte[]
            int len = payload.Length;
            int frameLen = FrameCodec.HeaderSize + len;
            if (_sendFrame.Length < frameLen)
                _sendFrame = new byte[Math.Max(frameLen, _sendFrame.Length * 2)];

            _sendFrame[0] = (byte)(len >> 24);
            _sendFrame[1] = (byte)(len >> 16);
            _sendFrame[2] = (byte)(len >> 8);
            _sendFrame[3] = (byte)len;
            Buffer.BlockCopy(payload, 0, _sendFrame, FrameCodec.HeaderSize, len);

            _stream.Write(_sendFrame, 0, frameLen);
        }
        catch (Exception e)
        {
            NetLog.Warn($"[TcpConnection] 发送失败：{e.Message}");
            MarkDisconnected();
        }
    }

    /// <summary>
    /// 发送（复用缓冲版本）：payload 写进复用 _sendFrame，避免 ToArray 再 new 一次。
    /// 高频发送（输入/快照）走这个，减少 GC 分配。
    /// </summary>
    public void Send(List<byte> payload)
    {
        if (!IsConnected) return;
        try
        {
            int len = payload.Count;
            int frameLen = FrameCodec.HeaderSize + len;
            if (_sendFrame.Length < frameLen)
                _sendFrame = new byte[Math.Max(frameLen, _sendFrame.Length * 2)];

            _sendFrame[0] = (byte)(len >> 24);
            _sendFrame[1] = (byte)(len >> 16);
            _sendFrame[2] = (byte)(len >> 8);
            _sendFrame[3] = (byte)len;
            for (int i = 0; i < len; i++)
                _sendFrame[FrameCodec.HeaderSize + i] = payload[i];

            _stream.Write(_sendFrame, 0, frameLen);
        }
        catch (Exception e)
        {
            NetLog.Warn($"[TcpConnection] 发送失败：{e.Message}");
            MarkDisconnected();
        }
    }

    /// <summary>主线程每帧调用：把队列里的消息取出来触发事件</summary>
    public void Poll()
    {
        while (true)
        {
            byte[] msg;
            lock (_lock)
            {
                if (_inbox.Count == 0) break;
                msg = _inbox.Dequeue();
            }
            OnMessage?.Invoke(msg);
        }

        if (_disconnected && !_disconnectEventFired)
        {
            _disconnectEventFired = true;
            OnDisconnected?.Invoke();
        }
    }

    private void ReceiveLoop()
    {
        var chunk = new byte[4096];
        try
        {
            while (_running)
            {
                int n = _stream.Read(chunk, 0, chunk.Length);
                if (n <= 0) break;   // 对端关闭连接

                lock (_lock)
                {
                    for (int i = 0; i < n; i++) _recvBuffer.Add(chunk[i]);
                    ExtractFrames();
                }
            }
        }
        catch (Exception e)
        {
            if (_running)
                NetLog.Warn($"[TcpConnection] 接收异常：{e.Message}");
        }
        MarkDisconnected();
    }

    /// <summary>把缓冲区里能拼成的完整帧全部取出来入队（粘包循环取，半包留到下次）</summary>
    private void ExtractFrames()
    {
        while (_recvBuffer.Count >= FrameCodec.HeaderSize)
        {
            if (!FrameCodec.TryExtract(_recvBuffer.ToArray(), _recvBuffer.Count, out var payload, out var consumed))
                break;   // 半包：等下次数据

            _recvBuffer.RemoveRange(0, consumed);
            _inbox.Enqueue(payload);
        }
    }

    private void MarkDisconnected()
    {
        _disconnected = true;
        _running = false;
    }

    public void Disconnect()
    {
        _running = false;
        try { _stream?.Close(); } catch { }
        try { _client?.Close(); } catch { }
        MarkDisconnected();
    }

    public void Dispose() => Disconnect();
}
