using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ATKingState : PlayerComboState
{

    public ATKingState(ActionStateMachine Asm):base(Asm){}

    public override void Enter()
    {
        base.Enter();
    }

    public override  void Exit()
    {
        base.Exit();
    }

    public override  void OnAnimationExitEvent()
    {
        if (ResuableData.hasBufferedInput)
        {
            ResuableData.hasBufferedInput = false;
            return;   // 留在 ComboState，不切走
        }
        
        // Asm.ComboIndex = 0;
        Asm.ChangeState(Asm.ActionNullState);
    }
    public override void OnAnimationTranslateEvent(IState newState)
    {
        Asm.ChangeState(newState);  // 默认：Animator 让切就切
    }

    public override  void Update(){}

    protected override void OnFireStarted(InputAction.CallbackContext ctx)
    {
        if (ResuableData.hasBufferedInput) return;
        //攻击窗口期判断
        float progress = Owner.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1;
        if (progress < Owner._inputWindowStart) return;
        if (ResuableData.comboIndex + 1 >= ResuableData.comboAnims.Length) return;

        ResuableData.hasBufferedInput = true;
        ComboNext();
    }

    //继续连招方法
    private void ComboNext()
    {
        ResuableData.comboIndex++;
        Owner.Animator.CrossFadeInFixedTime(ResuableData.comboAnims[ResuableData.comboIndex], 0.1f);
    }
}
