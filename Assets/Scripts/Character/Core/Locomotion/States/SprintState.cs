using UnityEngine;
using UnityEngine.InputSystem;

public class SprintState : LocomotionState
{
    private Vector2 _lastMoveValue;
    private bool _isTurning;
    private GameTimer _idleTimer;
    private const float TURN_ANGLE = 150f;
    private const float IDLE_BUFFER = 0.15f;

    public SprintState(LocomotionStateMachine sm) : base(sm) { }

    public override void Enter()
    {
        base.Enter();
        Owner.Animator.SetBool("HasInput", true);
        _lastMoveValue = Owner.MoveInput.MoveValue;
        _isTurning = false;
    }

    public override void Exit()
    {
        CancelIdleTimer();
        base.Exit();
    }

    public override void Update()
    {
        CheckTurnBack();
        base.Update();
    }

    protected override void AddInputCallbacks()
    {
        base.AddInputCallbacks();
        Owner.PlayerInput.actions["Player/Move"].canceled += OnMoveCanceled;
        Owner.PlayerInput.actions["Player/Move"].started  += OnMoveStarted;
    }

    protected override void RemoveInputCallbacks()
    {
        base.RemoveInputCallbacks();
        Owner.PlayerInput.actions["Player/Move"].canceled -= OnMoveCanceled;
        Owner.PlayerInput.actions["Player/Move"].started  -= OnMoveStarted;
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        if (_idleTimer != null) return;
        _idleTimer = TimerManager.Instance.GetTimer(IDLE_BUFFER, GoIdle);
    }

    private void OnMoveStarted(InputAction.CallbackContext ctx)
    {
        CancelIdleTimer();
    }

    private void GoIdle()
    {
        _idleTimer = null;
        if (!_isTurning)
            Sm.ChangeState(Sm.IdleState);
    }

    private void CancelIdleTimer()
    {
        if (_idleTimer != null)
        {
            TimerManager.Instance.Cancel(_idleTimer);
            _idleTimer = null;
        }
    }

    protected override float GetTargetSpeed()
    {
        return Owner.MoveInput.MoveValue.magnitude > 0.1f ? 3f : 0f;
    }

    private void CheckTurnBack()
    {
        Vector2 cur = Owner.MoveInput.MoveValue;
        if (_lastMoveValue.magnitude < 0.1f || cur.magnitude < 0.1f) return;

        if (Vector2.Angle(_lastMoveValue, cur) > TURN_ANGLE)
        {
            _isTurning = true;
            Owner.Animator.SetBool("TurnBack", true);
        }
        _lastMoveValue = cur;
    }
}
