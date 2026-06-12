using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ComboState : IState
{
    //是否有缓存输入
    private bool _hasBufferedInput;
    private string[] _comboAnims = { "Anbi_Normal_1", "Anbi_Normal_2", "Anbi_Normal_3" };
    protected readonly ActionStateMachine Sm;
    protected readonly PlayerController Owner;

    public ComboState(ActionStateMachine sm)
    {
        Sm = sm;
        Owner = sm.Owner;
    }

    public void Enter()
    {
        Owner.PlayerInput.actions["Player/Fire"].started += OnFireStarted;
        _hasBufferedInput = false;
        Sm.ComboIndex = 0;
    }

    public void Exit()
    {
        Owner.PlayerInput.actions["Player/Fire"].started -= OnFireStarted;
    }

    private void OnFireStarted(InputAction.CallbackContext ctx)
    {
        if (_hasBufferedInput) return;                          // 已经记过了
        
        float progress = Owner.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1;
        if (progress < Owner._inputWindowStart) return;         // 没到窗口
        
        if (Sm.ComboIndex + 1 >= _comboAnims.Length) return;    // 最后一招
        
        _hasBufferedInput = true;
    }

    public void OnAnimationExitEvent()
    {
        if (_hasBufferedInput)
        {
            _hasBufferedInput = false;
            Sm.ComboIndex++;
            Owner.Animator.CrossFadeInFixedTime(_comboAnims[Sm.ComboIndex], 0.1f);
            return;   // 留在 ComboState，不切走
        }
        
        Sm.ComboIndex = 0;
        Sm.ChangeState(Sm.ActionNullState);
    }
    public void OnAnimationTranslateEvent(IState newState)
    {
        Sm.ChangeState(newState);  // 默认：Animator 让切就切
    }

    public void Update(){}
}
