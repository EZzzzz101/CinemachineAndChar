using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ATKingState : PlayerComboState
{
    private bool _hasAdvancedCombo;//通过攻击方式切换
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
        //如果通过攻击方式切换则还在atking
        if (_hasAdvancedCombo)
        {
            _hasAdvancedCombo = false;
            return;   // 留在 ComboState，不切走
        }
        
        // Asm.ComboIndex = 0;
        Asm.ChangeState(Asm.ActionNullState);
    }
    public override void OnAnimationTranslateEvent(IState newState)
    {
        Asm.ChangeState(newState);  // 默认：Animator 让切就切
    }

    public override  void Update()
    {
        if (!ResuableData.hasBufferedInput) return;
         //攻击窗口期判断
        float progress = Owner.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1;
        if (progress < ResuableData.CurrentInputWindow) return;

        ComboNext();
    }

    protected override void OnFireStarted(InputAction.CallbackContext ctx)
    {
        if (_hasAdvancedCombo) return;           // ← 过渡中，禁止输入
        if (ResuableData.hasBufferedInput) return;
        if (ResuableData.comboIndex + 1 >= ResuableData.comboConfig.steps.Length) return;

        ResuableData.hasBufferedInput = true;    
    }

    //继续连招方法
    private void ComboNext()
    {
        ResuableData.hasBufferedInput = false;  // ← 先清，再播
        ResuableData.comboIndex++;
        _hasAdvancedCombo = true;     // ← 标记"刚切了"
        Owner.Animator.CrossFadeInFixedTime(ResuableData.CurrentAnimationName,0.1f);
        CharacterAudio.Instance.PlayComboSound(ResuableData.CurrentStep.attackSound);
        CharacterAudio.Instance.PlayComboVoice(ResuableData.CurrentStep.voiceClips);
    }
}
