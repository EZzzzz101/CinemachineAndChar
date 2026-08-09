using System;

/// <summary>
/// 输入抽象（M9）— 让 PlayerController 不再直接依赖 Input System。
/// 本地玩家 = LocalInputProvider（读键盘/手柄）；主机上的远端玩家 = RemoteInputProvider（消费网络上报的输入）。
/// 单机行为不变，遥控可驱动角色——这就是"输入与逻辑解耦"。
/// </summary>
public interface IInputProvider
{
    /// <summary>移动输入 X（连续值，键盘/摇杆合成）</summary>
    float MoveX { get; }
    /// <summary>移动输入 Z（连续值）</summary>
    float MoveZ { get; }
    /// <summary>是否按住冲刺键（连续状态）</summary>
    bool SprintHeld { get; }

    /// <summary>本帧刚推方向（边沿：Idle→Run 用）</summary>
    bool MoveStarted { get; }
    /// <summary>本帧刚松开方向（边沿：Run→Idle 用）</summary>
    bool MoveCanceled { get; }
    /// <summary>本帧刚按闪避（边沿）</summary>
    bool DashPressed { get; }
    /// <summary>本帧刚按攻击（边沿）</summary>
    bool AttackPressed { get; }

    /// <summary>
    /// 边沿事件回调：本地输入发生"按下"时触发（网络层借此即时上报）；
    /// 远端输入不需要（主机直接 Apply 给远端 provider），保持空实现即可。
    /// </summary>
    event Action<BattleInputFlags> OnEdge;

    /// <summary>帧开始：从输入源采集当前状态（远端实现为空，状态由 Apply 推入）</summary>
    void Tick();
    /// <summary>帧结束：消费掉本帧边沿，防止下一帧重复触发</summary>
    void EndFrame();
}
