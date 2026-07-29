using UnityEngine;

/// <summary>游戏整体状态</summary>
public enum GameState
{
    Overworld,       // 主世界（单人）
    DungeonLoading,  // 加载副本中
    Dungeon,         // 副本中（联机）
}

/// <summary>
/// 游戏状态管理器 — 控制主世界/副本的状态切换
/// 其他模块监听 GameStateChanged 事件做对应处理
/// </summary>
public class GameStateManager : GameModule<GameStateManager>
{
    public GameState State { get; private set; } = GameState.Overworld;

    protected override void OnInit()
    {
        State = GameState.Overworld;
        Debug.Log("[GameStateManager] 初始化完成");
    }

    public void ChangeState(GameState newState)
    {
        if (State == newState) return;
        State = newState;
        EventBus.Emit(GameEvents.GameStateChanged, newState);
    }
}
