using System;
using UnityEngine;

/// <summary>
/// 常驻网络层（大厅客户端服务）— 全局共享一个 LobbyClient。
/// 挂在 DontDestroyOnLoad 根上（GameModule 自带），切场景不断开。
/// 登录界面调 Connect(...)，组队界面调 Search/Invite，UI 只订阅事件，不直接碰网络类。
/// </summary>
public class LobbyClientService : GameModule<LobbyClientService>
{
    [Header("大厅服务器")]
    [Tooltip("服务器地址（本机调试 127.0.0.1；局域网填服务器机器 IP）")]
    [SerializeField] private string serverIp = "127.0.0.1";
    [Tooltip("大厅服务器端口（与 LobbyServer/Program.cs 默认一致）")]
    [SerializeField] private int serverPort = 7777;

    public LobbyClient Client { get; private set; }

    public bool Connected => Client != null && Client.Connected;
    public bool Registered => Client != null && Client.Registered;
    public string MyName => Client?.MyName;

    public string ServerIp => serverIp;
    public int ServerPort => serverPort;

    // ---- 转发给 UI 的事件（UI 订阅这里） ----
    public event Action<string> OnRegisterResult;         // 注册结果说明
    public event Action<bool, string> OnSearchResult;     // 是否在线、名字
    public event Action<string> OnInvited;                // 被谁邀请
    public event Action<string, string, string, int, int> OnJoinedRoom;   // 房主名、客人名、房主IP、端口、房间号
    public event Action<bool, string> OnInviteResult;     // 我发出的邀请结果
    public event Action<int> OnRoomStart;                 // 房间开始战斗（收到即进战斗场景）
    public event Action<string> OnError;                  // 错误信息
    public event Action OnDisconnected;                   // 与服务器断开

    protected override void OnInit()
    {
        EnsureClient();
        Debug.Log("[LobbyClientService] 初始化完成（未连接，登录界面触发连接）");
    }

    private void Update()
    {
        Client?.Poll();
    }

    /// <summary>登录时调用：连接大厅并注册用户名</summary>
    public void Connect(string userName)
    {
        EnsureClient();
        Client.Connect(serverIp, serverPort, userName);
    }

    public void Search(string keyword)
    {
        EnsureClient();
        Client.Search(keyword);
    }

    public void Invite(string targetName)
    {
        EnsureClient();
        Client.Invite(targetName);
    }

    public void ReplyInvite(bool accept)
    {
        EnsureClient();
        Client.ReplyInvite(accept);
    }

    /// <summary>房主点"进入游戏"：请求大厅通知全房间一起进战斗场景</summary>
    public void RequestStartBattle()
    {
        EnsureClient();
        Client.RequestStartBattle();
    }

    public void Disconnect()
    {
        Client?.Disconnect();
    }

    private void EnsureClient()
    {
        if (Client != null) return;

        Client = new LobbyClient();
        Client.OnRegisterResult += r => OnRegisterResult?.Invoke(r);
        Client.OnSearchResult += (f, n) => OnSearchResult?.Invoke(f, n);
        Client.OnInvited += n => OnInvited?.Invoke(n);
        Client.OnJoinedRoom += (hn, gn, ip, p, id) => OnJoinedRoom?.Invoke(hn, gn, ip, p, id);
        Client.OnInviteResult += (a, r) => OnInviteResult?.Invoke(a, r);
        Client.OnRoomStart += id => OnRoomStart?.Invoke(id);
        Client.OnError += e => OnError?.Invoke(e);
        Client.OnDisconnected += () => OnDisconnected?.Invoke();
    }
}
