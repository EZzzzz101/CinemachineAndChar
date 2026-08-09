public class IdleState : LocomotionState
{
    public IdleState(LocomotionStateMachine sm) : base(sm) { }

    public override void Enter()
    {
        base.Enter();
        Owner.Animator.SetBool("HasInput", false);
    }

    public override void Update()
    {
        base.Update();

        // Idle 只关心一件事：玩家开始推方向键 → 切到走路/跑步（轮询边沿，本地/远端通用）
        if (Owner.Input == null || !Owner.Input.MoveStarted) return;

        // 受击硬直锁定中：禁止走路（动画前 70% 站桩）
        if (Owner.IsInHitStun) return;

        // if (Owner.Input.SprintHeld)
        //     Sm.ChangeState(Sm.SprintState);
        // else
        //     Sm.ChangeState(Sm.RunState);
        Sm.ChangeState(Sm.RunState);
    }
}
