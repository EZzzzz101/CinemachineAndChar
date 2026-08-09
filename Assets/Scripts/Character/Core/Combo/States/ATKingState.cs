using UnityEngine;

public class ATKingState : PlayerComboState
{
    private bool _hasAdvancedCombo;//标记是否收刀

    /// <summary>攻击自动面向锁定目标的角速度（度/秒）</summary>
    private const float AutoFaceTurnSpeed = 720f;
    /// <summary>段切换后放开转向的脉冲时长（短暂可转，避免整段末尾放开造成滑步）</summary>
    private const float TurnPulseDuration = 0.15f;

    public ATKingState(ActionStateMachine Asm):base(Asm){}

    /// <summary>段切换脉冲结束的时间戳（此之前 = 可转向）</summary>
    private float _turnPulseUntil;

    /// <summary>
    /// 挥击中锁定转向；段切换（ComboNext）后给一个短暂脉冲放开，供玩家用移动输入瞄准下一段。
    /// 用"短暂脉冲"而非"整段末尾放开"，避免根运动滑步。
    /// </summary>
    public bool IsTurnLocked
    {
        get
        {
            // 段切换脉冲期间 → 放开（短暂可转向）
            if (Time.time < _turnPulseUntil) return false;
            return true;   // 其余挥击时间锁定
        }
    }

    public override void Enter()
    {
        base.Enter();
        ResuableData.canInput = false;        // 等 EnablePreInput() 打开（允许记录按下）
        ResuableData.canATK   = false;        // 等 CancelAttackColdTime() 打开（允许执行）
        ResuableData.canLinkCombo = true;     // 每段进状态重置，防止上一段的 DisableLinkCombo 残留
        _turnPulseUntil = 0f;                 // 起手段直接锁定转向（起手已 FaceEnemy 面向）
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
        // 攻击边沿轮询：窗口内按下 → 缓冲（等价原 OnFireStarted 回调）
        if (Owner.Input != null && Owner.Input.AttackPressed)
        {
            if (ResuableData.canInput && ResuableData.canLinkCombo && !ResuableData.hasBufferedInput)
                ResuableData.hasBufferedInput = true;
        }

        // 锁定转向期间自动面向锁定目标（绝区零式软锁定）；段切换脉冲期间让玩家用输入控制转向
        if (IsTurnLocked)
            AutoFaceEnemy();

        // 两段锁：canATK（CancelAttackColdTime 打开）+ hasBufferedInput（玩家按了）
        if (!ResuableData.canATK) return;
        if (!ResuableData.hasBufferedInput) return;

        ComboNext();
    }

    /// <summary>
    /// 攻击中自动面向锁定目标：锁定且目标在攻击范围内 → 平滑转过去。
    /// 范围外 / 未锁定 → 不干预，保持现有转向（HandleRotation）。
    /// </summary>
    private void AutoFaceEnemy()
    {
        if (!LockOnManager.HasInstance || !LockOnManager.Instance.IsLockedOn) return;
        var target = LockOnManager.Instance.CurrentTarget;
        if (target == null) return;

        Vector3 toTarget = target.GetLockOnPosition() - Owner.transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f) return;

        // 范围门：只在攻击范围内转（远了保持走位自由）
        float range = ResuableData.CurrentStep != null && ResuableData.CurrentStep.attackRange > 0f
            ? ResuableData.CurrentStep.attackRange : 2.5f;
        if (toTarget.sqrMagnitude > range * range) return;

        // 平滑快转（角速度 AutoFaceTurnSpeed，可调）
        Owner.transform.rotation = Quaternion.RotateTowards(
            Owner.transform.rotation,
            Quaternion.LookRotation(toTarget.normalized),
            AutoFaceTurnSpeed * Time.deltaTime
        );
    }

    //继续连招方法
    private void ComboNext()
    {
        ResuableData.hasBufferedInput = false;
        ResuableData.canInput = false;          // 关窗，等下一段 EnablePreInput
        ResuableData.canATK   = false;          // 关窗，等下一段 CancelAttackColdTime
        ResuableData.comboIndex = (ResuableData.comboIndex + 1) % ResuableData.comboConfig.steps.Length;  // 循环
        _turnPulseUntil = Time.time + TurnPulseDuration;   // 段切换：给一个短暂脉冲放开转向，供瞄准下一段
        Debug.Log("[lianzhao]:"+ResuableData.comboIndex);
        ResuableData.currentATKIndex = 0;
        _hasAdvancedCombo = true;
        Owner.Animator.CrossFadeInFixedTime(ResuableData.CurrentAnimationName,0.1f);
        Owner.PlayerAudio.PlayComboSound(ResuableData.CurrentStep.attackSound);
        Owner.PlayerAudio.PlayComboVoice(ResuableData.CurrentStep.voiceClips);
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

        // 命中检测 + 结算（复用共享工具 AttackHitHelper，与怪物攻击同一套命中逻辑）
        var combo = ResuableData.comboConfig;
        int mask = combo != null && combo.enemyLayer.value != 0 ? combo.enemyLayer.value : Physics.AllLayers;
        Vector3 origin = Owner.transform.position + Vector3.up * (step.attackUpOffset > 0f ? step.attackUpOffset : 1f);
        bool anyHit = AttackHitHelper.DealDamage(
            origin, Owner.transform.forward,
            step.attackRange, step.attackAngle, mask,
            step.damage, Owner.gameObject,
            Owner.transform,          // 排除自身：玩家现在是 IDamageable，防止打到自己
            Owner.baseAttribute);     // 传入玩家基础属性 → 参与暴击判定

        // 命中音效：至少打中一个目标才播（空挥不响）
        if (anyHit && step.hitSound != null)
        {
            Owner.PlayerAudio.PlayHitSound(step.hitSound);
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
 
