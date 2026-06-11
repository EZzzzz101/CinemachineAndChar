using UnityEngine;
using UnityEngine.InputSystem;

public class RunState : LocomotionState
{
    public RunState(LocomotionStateMachine sm) : base(sm) { }

    public override void Enter()
    {
        base.Enter();
        Owner.Animator.SetBool("HasInput", true);
    }

    protected override void AddInputCallbacks()
    {
        Owner.PlayerInput.actions["Player/Move"].canceled += OnMoveCanceled;
        Owner.PlayerInput.actions["Player/Dash"].started += OnSprintStarted;
    }

    protected override void RemoveInputCallbacks()
    {
        Owner.PlayerInput.actions["Player/Move"].canceled -= OnMoveCanceled;
        Owner.PlayerInput.actions["Player/Dash"].started  -= OnSprintStarted;
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        Sm.ChangeState(Sm.IdleState);
    }

    private void OnSprintStarted(InputAction.CallbackContext ctx)
    {
        Sm.ChangeState(Sm.SprintState);
    }

    protected override float GetTargetSpeed()
    {
        return Owner.MoveInput.MoveValue.magnitude > 0.1f ? 2f : 0f;
    }
}
