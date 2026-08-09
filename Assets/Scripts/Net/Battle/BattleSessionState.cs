using System.Collections.Generic;

/// <summary>
/// 战斗会话跨场景状态 — 大厅 JoinRoom 信息的中转站。
///
/// 为什么需要它：大厅服务器是独立进程，成员信息通过 JoinRoom 消息发给双方客户端；
/// 客户端进程需要把"房间里有谁、我是谁、谁是房主"跨场景保存（六分街组队 → 战斗场景），
/// 等战斗场景加载后再告诉 BattleServer / 用于自动连接。
/// 静态类 = 进程内常驻，切场景不丢；正式流程从大厅进入时填充，手动测试面板不用它。
/// </summary>
public static class BattleSessionState
{
    /// <summary>是否由大厅流程进入战斗（false = 手动调试面板）</summary>
    public static bool FromLobby;

    /// <summary>我是不是房主（MyName == HostName）</summary>
    public static bool IsHost;

    public static string MyName;
    public static string HostName;
    public static string HostIp;
    public static int HostPort = 7778;
    public static int RoomId;

    /// <summary>房间成员（含房主），列表顺序即槽位：0=房主，1+ 按成员顺序</summary>
    public static readonly List<string> MemberNames = new();

    public static void Reset()
    {
        FromLobby = false;
        IsHost = false;
        MyName = null;
        HostName = null;
        HostIp = null;
        HostPort = 7778;
        RoomId = 0;
        MemberNames.Clear();
    }
}
