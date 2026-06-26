using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ATKingState : PlayerComboState
{
    private bool _hasAdvancedCombo;//标记是否收刀
    public ATKingState(ActionStateMachine Asm):base(Asm){}

    public override void Enter()
    {
        base.Enter();
        ResuableData.canInput = false;        // 等 EnablePreInput() 打开（允许记录按下）
        ResuableData.canATK   = false;        // 等 CancelAttackColdTime() 打开（允许执行）
        ResuableData.canLinkCombo = true;     // 每段进状态重置，防止上一段的 DisableLinkCombo 残留
    }

    public override  void Exit()
    {
        base.Exit();
    }

    public override  void OnAnimationExitEvent()
    {
        if (_hasAdvancedCombo)
        {
            _hasAdvancedCombo = false;
            return;   // 留在 ComboState，不切走
        }
        Asm.ChangeState(Asm.ActionNullState);
    }
    public override void OnAnimationTranslateEvent(IState newState)
    {
        Asm.ChangeState(newState);  // 默认：Animator 让切就切
    }

    public override  void Update()
    {
        // 两段锁：canATK（CancelAttackColdTime 打开）+ hasBufferedInput（玩家按了）
        if (!ResuableData.canATK) return;
        if (!ResuableData.hasBufferedInput) return;

        ComboNext();
    }

    protected override void OnFireStarted(InputAction.CallbackContext ctx)
    {
        if (!ResuableData.canInput) return;              // 预输入窗口未开（等 EnablePreInput）
        if (!ResuableData.canLinkCombo) return;         // DisableLinkCombo() 禁止连招
        if (ResuableData.hasBufferedInput) return;

        ResuableData.hasBufferedInput = true;
    }

    //继续连招方法
    private void ComboNext()
    {
        ResuableData.hasBufferedInput = false;
        ResuableData.canInput = false;          // 关窗，等下一段 EnablePreInput
        ResuableData.canATK   = false;          // 关窗，等下一段 CancelAttackColdTime
        ResuableData.comboIndex = (ResuableData.comboIndex + 1) % ResuableData.comboConfig.steps.Length;  // 循环
        Debug.Log("[lianzhao]:"+ResuableData.comboIndex);
        ResuableData.currentATKIndex = 0;
        _hasAdvancedCombo = true;
        Owner.Animator.CrossFadeInFixedTime(ResuableData.CurrentAnimationName,0.1f);
        CharacterAudio.Instance.PlayComboSound(ResuableData.CurrentStep.attackSound);
        CharacterAudio.Instance.PlayComboVoice(ResuableData.CurrentStep.voiceClips);
    }

     #region 攻击事件（动画关键帧调用 → 委托到当前状态）
    public void EnablePreInput()
    {
        ResuableData.canInput = true;
        Debug.Log("[yv]预输入窗口开放");
    }

    public void CancelAttackColdTime()
    {
        // 攻击冷却结束 → 允许缓冲的指令执行
        ResuableData.canATK = true;
    }

    public void DisableLinkCombo()
    {
        ResuableData.canLinkCombo = false;
    }

    public void EnableMoveInterrupt()
    {
        ResuableData.canMoveInterrupt = true;
    }
    //核心事件，包括了伤害、受击动画、格挡攻击、攻击者、打击感（震屏、顿帧）、受击音效、受击特效
    public void ATK()
    {
        var step = ResuableData.CurrentStep;
        int idx = ResuableData.currentATKIndex;

        // 震屏
        if (step.shakeForceList != null && idx < step.shakeForceList.Length)
        {
            float force = step.shakeForceList[idx];
            if (force > 0f) CameraShake.Instance.TriggerShake(force);
        }
        // 顿帧
        if (step.hitPauseList != null && idx < step.hitPauseList.Length)
        {
            float duration = step.hitPauseList[idx];
            if (duration > 0f) HitPauseManager.Instance.Trigger(duration, step.hitPauseScale);
        }

        ResuableData.currentATKIndex++;
    }
    #endregion
}
 