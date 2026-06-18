using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
        AddInputCallbacks();
    }

    public virtual void Exit()
    {
        RemoveInputCallbacks();
    }

    public virtual  void OnAnimationExitEvent()
    {

    }

    public virtual  void OnAnimationTranslateEvent(IState newState)
    {
  
    }

    public virtual  void Update()
    {

    }

    protected virtual void AddInputCallbacks()
    {
        Owner.PlayerInput.actions["Player/Fire"].started += OnFireStarted;
    }
    protected virtual void RemoveInputCallbacks()
    {
        Owner.PlayerInput.actions["Player/Fire"].started -= OnFireStarted;
    }

    protected virtual void OnFireStarted(InputAction.CallbackContext ctx)
    {
        Owner.Animator.CrossFadeInFixedTime(ResuableData.comboAnims[ResuableData.comboIndex], 0.111f);
    }
}
