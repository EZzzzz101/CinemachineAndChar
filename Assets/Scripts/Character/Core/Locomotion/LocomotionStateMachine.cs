/// <summary>
/// Locomotion 状态机 — 创建所有移动状态，暴露给外部
/// </summary>
public class LocomotionStateMachine : StateMachine
{
    public PlayerController Owner { get; }

    public IdleState          IdleState          { get; }
    public RunState           RunState           { get; }
    // public SprintState        SprintState        { get; }
    // public MovementNullState  MovementNullState  { get; }

    public LocomotionStateMachine(PlayerController owner)
    {
        Owner = owner;
        IdleState         = new IdleState(this);
        RunState          = new RunState(this);
        // SprintState       = new SprintState(this);
        // MovementNullState = new MovementNullState(this);
    }
}