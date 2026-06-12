using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Locomotion 状态基类
/// 提供所有移动状态共享的东西：读输入、转向
/// 子类重写 AddInputCallbacks / RemoveInputCallbacks 来决定自己在什么输入下切状态
/// </summary>
public abstract class LocomotionState : IState
{
    protected float _currentSpeed;
    protected float _speedVelocity;
    protected readonly LocomotionStateMachine Sm;
    protected readonly PlayerController Owner;

    protected LocomotionState(LocomotionStateMachine sm)
    {
        Sm = sm;
        Owner = sm.Owner;
    }

    public virtual void Enter()
    {
        AddInputCallbacks();
    }

    public virtual void Exit()
    {
        RemoveInputCallbacks();
    }

    /// <summary>子类在这里只订阅自己关心的输入</summary>
    protected virtual void AddInputCallbacks()
    {
        Owner.PlayerInput.actions["Player/Dash"].started += OnDashStarted;
    }
    protected virtual void RemoveInputCallbacks()
    {
        Owner.PlayerInput.actions["Player/Dash"].started -= OnDashStarted;
    }

    // 子类可以覆写，比如后面限制某些条件不能冲刺
    protected virtual void OnDashStarted(InputAction.CallbackContext ctx)
    {
        if(Owner.MoveInput.MoveValue.magnitude>0.1f)
            Owner.Animator.CrossFadeInFixedTime("DashFront", 0.1555f);
        else
            Owner.Animator.CrossFadeInFixedTime("DashBack", 0.1555f);
    }

    //获取目标速度虚函数
    protected virtual float GetTargetSpeed() => 0f;
    
    public virtual void Update()
    {   
        float targetSpeed =GetTargetSpeed();

         _currentSpeed = Mathf.SmoothDamp(
            _currentSpeed, targetSpeed,
            ref _speedVelocity, Owner.SpeedSmoothTime
        );
        // 驱动动画
        Owner.Animator.SetFloat("Movement", _currentSpeed);
        // 转向
        Owner.HandleRotation();
    }

    public virtual void OnAnimationTranslateEvent(IState newState)
    {
        Sm.ChangeState(newState);  // 默认：Animator 让切就切
    }

    public virtual void OnAnimationExitEvent() { }
}