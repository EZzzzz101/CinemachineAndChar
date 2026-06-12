using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SprintState : LocomotionState
{
    public SprintState (LocomotionStateMachine sm) : base(sm) { }

    public override void Enter()
    {
        base.Enter();
        Owner.Animator.SetBool("HasInput", true);
    }

    public override void Update()
    {
        base.Update();
    }

    protected override void AddInputCallbacks()
    {
        base.AddInputCallbacks();
        Owner.PlayerInput.actions["Player/Move"].canceled += OnMoveCanceled;
    }

    protected override void RemoveInputCallbacks()
    {
        base.RemoveInputCallbacks();
        Owner.PlayerInput.actions["Player/Move"].canceled -= OnMoveCanceled;
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        Sm.ChangeState(Sm.IdleState);
    }

    protected override float GetTargetSpeed()
    {
        return Owner.MoveInput.MoveValue.magnitude > 0.1f ? 3f : 0f;
    }
}
