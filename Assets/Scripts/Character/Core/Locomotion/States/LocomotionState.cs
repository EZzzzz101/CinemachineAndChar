using UnityEngine;

/// <summary>
/// Locomotion 状态基类
/// 提供所有移动状态共享的东西：读输入、转向
/// 子类重写 AddInputCallbacks / RemoveInputCallbacks 来决定自己在什么输入下切状态
/// </summary>
public abstract class LocomotionState : IState
{
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

    /// <summary>子类在这里只订阅自己关心的输入（比如 Idle 关心 Movement.started）</summary>
    protected virtual void AddInputCallbacks() { }
    protected virtual void RemoveInputCallbacks() { }

    public virtual void Update()
    {
        // 每帧设置动画参数
        Owner.Animator.SetFloat("Movement", Owner.MoveInput.MoveValue.magnitude);
        // 转向
        Owner.HandleRotation();
    }

    public virtual void OnAnimationTranslateEvent(IState newState)
    {
        Sm.ChangeState(newState);  // 默认：Animator 让切就切
    }

    public virtual void OnAnimationExitEvent() { }
}