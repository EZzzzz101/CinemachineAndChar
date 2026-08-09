using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnBackState : LocomotionState
{
    public TurnBackState(LocomotionStateMachine sm) : base(sm) { }

    public override void Enter()
    {
        base.Enter();
        Owner.Animator.SetBool("TurnBack", false);
    }

    public override void OnAnimationExitEvent()
    {
        if (Owner.MoveValue.magnitude <0.1f)
        {
            Sm.ChangeState(Sm.IdleState);
            return;
        }
        Sm.ChangeState(Sm.SprintState);
    }
}
