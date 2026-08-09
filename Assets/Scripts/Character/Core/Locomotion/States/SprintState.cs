using UnityEngine;

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
        _lastMoveValue = Owner.MoveValue;
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

        // 轮询边沿替代输入回调：松方向 → 缓冲 0.15s 后回 Idle（防急停/误触）；再推方向 → 取消缓冲
        if (Owner.Input != null && Owner.Input.MoveCanceled && _idleTimer == null)
            _idleTimer = TimerManager.Instance.GetTimer(IDLE_BUFFER, GoIdle);
        if (Owner.Input != null && Owner.Input.MoveStarted)
            CancelIdleTimer();

        base.Update();
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
        return Owner.MoveValue.magnitude > 0.1f ? 3f : 0f;
    }

    private void CheckTurnBack()
    {
        Vector2 cur = Owner.MoveValue;
        if (_lastMoveValue.magnitude < 0.1f || cur.magnitude < 0.1f) return;

        if (Vector2.Angle(_lastMoveValue, cur) > TURN_ANGLE)
        {
            _isTurning = true;
            Owner.Animator.SetBool("TurnBack", true);
        }
        _lastMoveValue = cur;
    }
}
