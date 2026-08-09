using System;

/// <summary>
/// 远端输入实现（纯 C#，主机侧使用）— 主机模拟"别人的角色"时，
/// 把客户端上报的 BattleInputState 喂进来，让同一个 PlayerController 被遥控驱动。
/// 关键点：
/// - 摇杆是连续状态，Apply 直接覆盖；
/// - 闪避/攻击是"按下事件"，锁存到下一帧 EndFrame 才消费（与本地"本帧按下"语义对齐）；
/// - 方向边沿（MoveStarted/Canceled）由"有无输入"变化推导，供 Idle/Run 状态切换。
/// </summary>
public class RemoteInputProvider : IInputProvider
{
    private bool _hadMove;

    public float MoveX { get; private set; }
    public float MoveZ { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool MoveStarted { get; private set; }
    public bool MoveCanceled { get; private set; }
    public bool DashPressed { get; private set; }
    public bool AttackPressed { get; private set; }

    /// <summary>
    /// 远端输入不需要再转发（它已经是网络来的），但接口要求保留该事件。
    /// 用自定义空 add/remove 占位：既满足接口，也避免"从未使用"告警。
    /// </summary>
    public event Action<BattleInputFlags> OnEdge { add { } remove { } }

    public bool HasInput => (MoveX * MoveX + MoveZ * MoveZ) > 0.01f;

    /// <summary>主机每收到一条 BattleInput 消息时调用（主线程）</summary>
    public void Apply(BattleInputState input)
    {
        MoveX = input.MoveX;
        MoveZ = input.MoveZ;
        SprintHeld = (input.Flags & BattleInputFlags.Sprint) != 0;

        // 方向边沿：由移动量 无→有 / 有→无 推导
        bool hasNow = HasInput;
        // 边沿必须"锁存"（|=）：主机一帧可能收到多条输入消息（主机帧率低于客户端时），
        // 若直接赋值，后一条会把前一条刚置位的边沿覆盖回 false → "松开"事件丢失
        // → 状态机卡在 RunState 收不到 MoveCanceled → 角色永远在跑（Walk 循环漂移）。
        // 锁存后边沿保持到 EndFrame 才清除，保证状态机一定能读到。
        MoveStarted |= (!_hadMove && hasNow);
        MoveCanceled |= (_hadMove && !hasNow);
        _hadMove = hasNow;

        // 事件边沿：锁存（OR 合并），等 FSM 消费后 EndFrame 清掉
        if ((input.Flags & BattleInputFlags.Dash) != 0) DashPressed = true;
        if ((input.Flags & BattleInputFlags.Attack) != 0) AttackPressed = true;
    }

    public void Tick()
    {
        // 状态由 Apply 推入，无需采集
    }

    public void EndFrame()
    {
        MoveStarted = false;
        MoveCanceled = false;
        DashPressed = false;
        AttackPressed = false;
    }

    /// <summary>
    /// 输入超时清零：客户端失焦/卡顿/断连时停止上报，若不清理，
    /// 主机上的远端角色会保留最后输入一直移动（"自己向前走"的漂移根源）。
    /// 由 BattleHostRuntime 定时调用。
    /// </summary>
    public void ClearInput()
    {
        MoveX = 0f;
        MoveZ = 0f;
        SprintHeld = false;
        MoveStarted = false;
        MoveCanceled = false;
        DashPressed = false;
        AttackPressed = false;
        _hadMove = false;
    }
}
