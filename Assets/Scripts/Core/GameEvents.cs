/// <summary>
/// 事件名称常量 — 所有 EventBus 消息都在这里定义
/// 跨模块通信用，UI内部操作不放这里
/// </summary>
public static class GameEvents
{
    // ---- 玩家 ----
    public const string PlayerDied        = "player.died";       // 角色死亡 → 失败结算界面
    public const string PlayerRevived     = "player.revived";


    // ---- 战斗 ----
    public const string HitLanded         = "hit.landed";       // 命中（触发伤害数字）
    public const string EnemyDied         = "enemy.died";        // 怪物死亡 → 胜利结算界面
    public const string EnemySpawned      = "enemy.spawned";

    public const string HPChanged   = "hp.changed";

    public const string HPTextChanged   = "hptext.changed";


    // ---- 副本 ----
    public const string DungeonStarted    = "dungeon.started";
    public const string DungeonComplete   = "dungeon.complete";
    public const string DungeonFailed     = "dungeon.failed";
    public const string DungeonExited     = "dungeon.exited";

    // ---- 网络 ----
    public const string PlayerConnected   = "network.player.connected";
    public const string PlayerDisconnected = "network.player.disconnected";

    // ---- 游戏状态 ----
    public const string GameStateChanged  = "game.state.changed";
}
