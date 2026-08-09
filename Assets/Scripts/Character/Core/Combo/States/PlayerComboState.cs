using UnityEngine;

public class PlayerComboState : IState
{
    protected ActionStateMachine Asm {get;}
    protected PlayerController Owner {get;}
    protected ComboResuableData ResuableData { get; }

    public PlayerComboState(ActionStateMachine actionStateMachine)
    {
        Asm=actionStateMachine;
        Owner=actionStateMachine.Owner;
        ResuableData=actionStateMachine.ResuableData;
    }
        
    public virtual void Enter()
    {
    }

    public virtual void Exit()
    {
    }

    public virtual  void OnAnimationExitEvent()
    {

    }

    public virtual  void OnAnimationTranslateEvent(IState newState)
    {
  
    }

    public virtual void Update()
    {
        // M9：攻击边沿统一轮询（原 InputAction started 回调改到这里），本地/远端透明
        if (Owner.Input != null && Owner.Input.AttackPressed)
            OnFirePressed();
    }

    /// <summary>鼠标左键攻击：默认动作 = 播第一段攻击动画（子类覆写做连招/起手逻辑）</summary>
    protected virtual void OnFirePressed()
    {
        Owner.Animator.CrossFadeInFixedTime(ResuableData.CurrentAnimationName, 0.111f);
    }
}
