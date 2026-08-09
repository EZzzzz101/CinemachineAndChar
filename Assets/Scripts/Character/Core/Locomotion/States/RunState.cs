using UnityEngine;

public class RunState : LocomotionState
{
    public RunState(LocomotionStateMachine sm) : base(sm) { }

    public override void Enter()
    {
        base.Enter();
        Owner.Animator.SetBool("HasInput", true);
    }

    public override void Update()
    {
        base.Update();

        // 松开方向 → 回 Idle（轮询边沿）
        if (Owner.Input != null && Owner.Input.MoveCanceled)
            Sm.ChangeState(Sm.IdleState);
    }

    protected override float GetTargetSpeed()
    {
        return Owner.MoveValue.magnitude > 0.1f ? 2f : 0f;
    }
}
