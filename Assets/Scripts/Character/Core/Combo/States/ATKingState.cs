using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ATKingState : PlayerComboState
{
    private bool _hasAdvancedCombo;//标记是否收刀

    /// <summary>攻击自动面向锁定目标的角速度（度/秒）</summary>
    private const float AutoFaceTurnSpeed = 720f;

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
        // 攻击中自动面向锁定目标（绝区零式软锁定）：让命中锥形始终对准
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

        // 命中检测：OverlapSphere + 角度过滤 + IDamageable + 去重
        var hitTargets = DetectHits(step);
        foreach (var damageable in hitTargets)
        {
            if (damageable == null) continue;
            damageable.TakeDamage(step.damage, Owner.gameObject);

            // 受击特效（配置了才播，2s 自毁）
            if (step.hitVfxPrefab != null)
            {
                var comp = damageable as Component;
                Vector3 hitPos = comp != null
                    ? comp.transform.position + Vector3.up * 1f
                    : Owner.transform.position + Owner.transform.forward * 1f;
                GameObject vfx = Object.Instantiate(step.hitVfxPrefab, hitPos, Quaternion.identity);
                Object.Destroy(vfx, 2f);
            }

            // 供以后伤害数字/统计订阅（无订阅者时安全 no-op）
            EventBus.Emit(GameEvents.HitLanded, step.damage);
        }

        // 顿帧
        if (step.hitPauseList != null && idx < step.hitPauseList.Length)
        {
            float duration = step.hitPauseList[idx];
            if (duration > 0f) HitPauseManager.Instance.Trigger(duration, step.hitPauseScale);
        }

        ResuableData.currentATKIndex++;
    }

    // OverlapSphere(按 Enemy 层) → 角度过滤前方锥形 → 只留挂 IDamageable 的
    private HashSet<IDamageable> DetectHits(ComboStepData step)
    {
        var result = new HashSet<IDamageable>();           // 按引用去重：多碰撞体只结算一次
        float range = step.attackRange > 0f ? step.attackRange : 2.5f;
        float halfAngle = (step.attackAngle > 0f ? step.attackAngle : 80f) * 0.5f;
        Vector3 origin = Owner.transform.position + Vector3.up * (step.attackUpOffset > 0f ? step.attackUpOffset : 1f);
        // 敌人层在 ComboConfigSO（整段连招共用）；未配则查所有层（靠下面的接口过滤兜底，玩家/地面没 IDamageable 自动跳过）
        var combo = ResuableData.comboConfig;
        LayerMask layer = combo != null ? combo.enemyLayer : default;
        int mask = layer.value != 0 ? layer.value : Physics.AllLayers;

        Collider[] cols = Physics.OverlapSphere(origin, range, mask);
        foreach (var col in cols)
        {
            if (col == null) continue;

            Vector3 toTarget = col.transform.position - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > range * range) continue;
            if (Vector3.Angle(Owner.transform.forward, toTarget.normalized) > halfAngle) continue;

            var damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            result.Add(damageable);
        }
        return result;
    }
    #endregion
}
 