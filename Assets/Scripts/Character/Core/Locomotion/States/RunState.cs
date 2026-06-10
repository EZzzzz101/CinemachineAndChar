using UnityEngine;
using UnityEngine.InputSystem;

public class RunState : LocomotionState
{
    private float _currentSpeed;
    private float _speedVelocity;

    public RunState(LocomotionStateMachine sm) : base(sm) { }

    public override void Enter()
    {
        base.Enter();
        Owner.Animator.SetBool("HasInput", true);
    }

    public override void Update()
    {
        base.Update();

        float inputMag = Owner.MoveInput.MoveValue.magnitude;

        // 速度计算
        float targetSpeed = inputMag > 0.1f ? 2f : 0f;
        if (Owner.MoveInput.IsSprinting) targetSpeed = 3f;

        _currentSpeed = Mathf.SmoothDamp(
            _currentSpeed, targetSpeed,
            ref _speedVelocity, Owner.SpeedSmoothTime
        );

        // 驱动动画
        Owner.Animator.SetFloat("Movement", _currentSpeed);
    }

    protected override void AddInputCallbacks()
    {
        Owner.PlayerInput.actions["Player/Move"].canceled += OnMoveCanceled;
    }

    protected override void RemoveInputCallbacks()
    {
        Owner.PlayerInput.actions["Player/Move"].canceled -= OnMoveCanceled;
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        Sm.ChangeState(Sm.IdleState);
    }
}
