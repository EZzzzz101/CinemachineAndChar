using UnityEngine;

/// <summary>
/// Locomotion 状态基类
/// 提供所有移动状态共享的东西：读输入（IInputProvider 轮询）、转向。
/// M9 之后不再订阅 InputSystem 事件：边沿统一在 Update 里轮询，
/// 本地/远端输入对状态机完全透明。
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
    }

    public virtual void Exit()
    {
    }

    //获取目标速度虚函数
    protected virtual float GetTargetSpeed() => 0f;

    public virtual void Update()
    {
        HandleDashInput();

        float targetSpeed = GetTargetSpeed();

        // Animator 内置阻尼，比 Mathf.SmoothDamp 更丝滑，不影响 root motion
        Owner.Animator.SetFloat("Movement", targetSpeed, Owner.SpeedSmoothTime, Time.deltaTime);
        // 攻击挥击进行中/受击锁定中：不随移动输入转（挥击段末放开供控制下一段；受击站桩）
        if (!Owner.IsTurnLocked && !Owner.IsInHitStun)
            Owner.HandleRotation();
    }

    /// <summary>
    /// 闪避边沿轮询（原 InputAction started 回调改到这里）：
    /// 所有移动状态共享；受击硬直禁闪、冲刺未到 70% 禁连闪。
    /// </summary>
    private void HandleDashInput()
    {
        if (Owner.Input == null || !Owner.Input.DashPressed) return;

        // 受击硬直中禁止冲刺，强制看受击动画
        if (Owner.IsInHitStun) return;

        // 冲刺未到 70% 不允许再次冲刺（防无限连冲）
        if (Owner.Locomotion != null && Owner.Locomotion.CurrentState is DashingState)
        {
            var info = Owner.Animator.GetCurrentAnimatorStateInfo(0);
            if (info.normalizedTime < DashChainThreshold) return;
        }

        if (Owner.MoveValue.magnitude > 0.1f)
            Owner.Animator.CrossFadeInFixedTime("DashFront", 0.1555f);
        else
            Owner.Animator.CrossFadeInFixedTime("DashBack", 0.1555f);
    }

    public virtual void OnAnimationTranslateEvent(IState newState)
    {
        Sm.ChangeState(newState);  // 默认：Animator 让切就切
    }

    public virtual void OnAnimationExitEvent() { }
}
