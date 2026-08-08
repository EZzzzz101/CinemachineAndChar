using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

/// <summary>
/// TCP 服务器 — 监听端口，Accept 新连接，管理所有在线连接。
/// 线程模型与 TcpConnection 一致：Accept 线程把新连接入队，
/// 主线程每帧 Poll() 取出并触发 OnClientConnected，再顺手 Poll 所有连接。
/// </summary>
public class TcpServer : IDisposable
{
    private readonly object _lock = new();
    private readonly Queue<TcpConnection> _pending = new();
    private readonly List<TcpConnection> _connections = new();

    private TcpListener _listener;
    private Thread _acceptThread;
    private volatile bool _running;

    public int Port { get; private set; }

    /// <summary>当前所有连接（主线程 Poll 时更新）</summary>
    public IReadOnlyList<TcpConnection> Connections => _connections;

    /// <summary>新客户端连上来（主线程 Poll 时触发）</summary>
    public event Action<TcpConnection> OnClientConnected;

    /// <summary>开始监听端口；失败返回 false（端口被占用、防火墙等）</summary>
    public bool Start(int port)
    {
        try
        {
            Port = port;
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _running = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true };
            _acceptThread.Start();
            NetLog.Log($"[TcpServer] 开始监听 :{port}");
            return true;
        }
        catch (Exception e)
        {
            NetLog.Error($"[TcpServer] 监听 {port} 失败：{e.Message}");
            return false;
        }
    }

    private void AcceptLoop()
    {
        try
        {
            while (_running)
            {
                var client = _listener.AcceptTcpClient();   // 阻塞：等下一个连接
                var conn = new TcpConnection();
                conn.Attach(client);                        // 把新连接接上收包线程
                lock (_lock) _pending.Enqueue(conn);        // 入队，等主线程领走
            }
        }
        catch (Exception e)
        {
            if (_running)
                NetLog.Warn($"[TcpServer] Accept 异常：{e.Message}");
        }
    }

    /// <summary>主线程每帧调用：处理新连接 + 各连接的收包队列</summary>
    public void Poll()
    {
        // 1. 把 Accept 线程排进来的新连接领走
        while (true)
        {
            TcpConnection conn;
            lock (_lock)
            {
                if (_pending.Count == 0) break;
                conn = _pending.Dequeue();
            }
            _connections.Add(conn);
            OnClientConnected?.Invoke(conn);
        }

        // 2. 清理已断开的连接，并让每个连接把消息队列分发出去
        for (int i = _connections.Count - 1; i >= 0; i--)
        {
            var conn = _connections[i];
            // 先 Poll：既分发消息，也触发 OnDisconnected（断线事件在 Poll 里发出）
            conn.Poll();
            if (!conn.IsConnected)
            {
                _connections.RemoveAt(i);
            }
        }
    }

    /// <summary>停服：关闭监听 + 断开所有连接</summary>
    public void Stop()
    {
        _running = false;
        try { _listener?.Stop(); } catch { }
        foreach (var c in _connections) c.Disconnect();
        _connections.Clear();
        lock (_lock) _pending.Clear();
    }

    public void Dispose() => Stop();
}
