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

    /// <summary>冲刺动画播放到多少进度才允许再次冲刺（0.7 = 冲到 70% 才开放新冲刺窗口，防无限连冲）</summary>
    private const float DashChainThreshold = 0.7f;

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
        // 受击硬直中禁止冲刺，强制看受击动画
        if (Owner.IsInHitStun) return;

        // 冲刺未到 70% 不允许再次冲刺（防无限连冲）
        if (Owner.Locomotion != null && Owner.Locomotion.CurrentState is DashingState)
        {
            var info = Owner.Animator.GetCurrentAnimatorStateInfo(0);
            if (info.normalizedTime < DashChainThreshold) return;
        }

        if(Owner.MoveInput.MoveValue.magnitude>0.1f)
            Owner.Animator.CrossFadeInFixedTime("DashFront", 0.1555f);
        else
            Owner.Animator.CrossFadeInFixedTime("DashBack", 0.1555f);
    }

    //获取目标速度虚函数
    protected virtual float GetTargetSpeed() => 0f;
    
    public virtual void Update()
    {
        float targetSpeed = GetTargetSpeed();

        // Animator 内置阻尼，比 Mathf.SmoothDamp 更丝滑，不影响 root motion
        Owner.Animator.SetFloat("Movement", targetSpeed, Owner.SpeedSmoothTime, Time.deltaTime);
        // 攻击挥击进行中/受击锁定中：不随移动输入转（挥击段末放开供控制下一段；受击站桩）
        if (!Owner.IsTurnLocked && !Owner.IsInHitStun)
            Owner.HandleRotation();
    }

    public virtual void OnAnimationTranslateEvent(IState newState)
    {
        Sm.ChangeState(newState);  // 默认：Animator 让切就切
    }

    public virtual void OnAnimationExitEvent() { }
}