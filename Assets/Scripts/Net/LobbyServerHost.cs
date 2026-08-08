using UnityEngine;

/// <summary>
/// 大厅服务器宿主 — 把 LobbyServer 挂进场景/进程。
/// Awake 启动监听（保证先于客户端 Start 连接），每帧 Poll 驱动接客 + 消息分发，OnDestroy 关服。
/// </summary>
public class LobbyServerHost : MonoBehaviour
{
    [Header("大厅服务器")]
    [SerializeField] private int port = 7777;
    [SerializeField] private bool autoStart = true;

    private LobbyServer _server;

    public LobbyServer Server => _server;
    public int Port => port;

    private void Awake()
    {
        if (autoStart) StartServer();
    }

    private void Update()
    {
        _server?.Poll();   // 服务器每帧都要被驱动，否则消息永远不处理
    }

    [ContextMenu("启动大厅服务器")]
    public void StartServer()
    {
        if (_server != null) return;
        _server = new LobbyServer();
        if (!_server.Start(port))
        {
            Debug.LogError($"[LobbyHost] 大厅服务器启动失败 :{port}");
            _server = null;
        }
    }

    private void OnDestroy()
    {
        _server?.Stop();
        _server = null;
    }
}
