using UnityEngine.InputSystem;

public class IdleState : LocomotionState
{
    public IdleState(LocomotionStateMachine sm) : base(sm) { }

    public override void Enter()
    {
        base.Enter();
        Owner.Animator.SetBool("HasInput", false);
    }

    /// <summary>Idle 只关心一件事：玩家开始推方向键 → 切到走路或跑步</summary>
    protected override void AddInputCallbacks()
    {
        base.AddInputCallbacks();
        Owner.PlayerInput.actions["Player/Move"].started += OnMoveStarted;
    }

    protected override void RemoveInputCallbacks()
    {
        base.RemoveInputCallbacks();
        Owner.PlayerInput.actions["Player/Move"].started -= OnMoveStarted;
    }

    private void OnMoveStarted(InputAction.CallbackContext ctx)
    {
        // if (Owner.MoveInput.IsSprinting)
        //     Sm.ChangeState(Sm.SprintState);
        // else
        //     Sm.ChangeState(Sm.RunState);
         Sm.ChangeState(Sm.RunState);
    }
}