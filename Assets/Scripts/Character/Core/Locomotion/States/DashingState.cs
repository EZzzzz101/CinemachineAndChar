using UnityEngine;

public class DashingState : LocomotionState
{
    public DashingState (LocomotionStateMachine sm) : base(sm) { }

    public override void Enter()
    {
        base.Enter();
        CharacterAudio.Instance.PlayDodgeSound(Owner.LastAnimEnterState);
        Debug.Log($"[DashingState] Enter via: {Owner.LastAnimEnterState}");
    }

    #region Dash转到 Idle?Sprint
    public override void OnAnimationExitEvent()
    {
        if (Owner.MoveInput.MoveValue.magnitude <0.1f)
        {
            Sm.ChangeState(Sm.IdleState);
            return;
        }
        Sm.ChangeState(Sm.SprintState);
    }
    #endregion


}
