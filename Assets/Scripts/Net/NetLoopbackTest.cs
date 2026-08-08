using UnityEngine;

/// <summary>
/// 传输层自测：同一进程起服务器 + 客户端连自己（127.0.0.1 回环）。
/// 验证链路：客户端发 → 服务器收到并回 → 客户端再收到。
/// 以后多进程测试：一个进程只起 Server，另一个只 Connect。
/// </summary>
public class NetLoopbackTest : MonoBehaviour
{
    [Header("网络自测")]
    [SerializeField] private int port = 7777;
    [SerializeField] private bool autoStart = true;

    private TcpServer _server;
    private TcpConnection _client;
    private int _receivedCount;

    private void Start()
    {
        if (autoStart) StartTest();
    }

    [ContextMenu("开始自测")]
    public void StartTest()
    {
        _receivedCount = 0;

        // 1. 起服务器
        _server = new TcpServer();
        _server.OnClientConnected += OnServerGotClient;
        _server.Start(port);

        // 2. 客户端连自己（回环）
        _client = new TcpConnection();
        _client.OnMessage += OnClientGotMessage;
        if (_client.Connect("127.0.0.1", port))
        {
            var hello = System.Text.Encoding.UTF8.GetBytes("你好，传输层");
            _client.Send(hello);
            Debug.Log("[NetTest] 客户端已发送消息");
        }
    }

    private void Update()
    {
        // 主线程每帧驱动：服务器和客户端都要 Poll
        _server?.Poll();
        _client?.Poll();
    }

    private void OnServerGotClient(TcpConnection conn)
    {
        Debug.Log("[NetTest] 服务器收到客户端连接");
        conn.OnMessage += msg =>
        {
            var text = System.Text.Encoding.UTF8.GetString(msg);
            Debug.Log($"[NetTest] 服务器收到：{text}");
            conn.Send(System.Text.Encoding.UTF8.GetBytes($"服务器回复：{text}"));
        };
    }

    private void OnClientGotMessage(byte[] msg)
    {
        _receivedCount++;
        var text = System.Text.Encoding.UTF8.GetString(msg);
        Debug.Log($"[NetTest] 客户端收到：{text}（累计 {_receivedCount} 条）");
    }

    private void OnDestroy()
    {
        _client?.Disconnect();
        _server?.Stop();
    }
}