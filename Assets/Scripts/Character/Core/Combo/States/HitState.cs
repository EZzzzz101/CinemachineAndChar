using UnityEngine;

/// <summary>
/// 受击硬直状态 — 播受击动画(BeHit)、锁攻击输入。
/// 退出方式：动画切走（Animator 离开 BeHit，如播完回 Idle / 被闪避顶掉 / 推方向回 Locomotion）→ 回 ActionNullState；
/// 超时兜底防卡死。若动画侧给 BeHit 挂了 AnimationExitBehaviour，也会走 PlayerController.OnAnimationExitEvent。
/// </summary>
public class HitState : PlayerComboState
{
    /// <summary>防御性超时：动画事件没接上/退不出时强制恢复，防卡死</summary>
    private const float MaxDuration = 3f;
    /// <summary>CrossFade 过渡期(秒)：过渡期间当前状态还是旧状态，不算被打断</summary>
    private const float MinStayTime = 0.2f;
    /// <summary>受击动画播放到多少进度后放开操作（0.7 = 前 70% 锁死，后 30% 可走/可闪）</summary>
    private const float LockReleaseTime = 0.7f;

    private float _enterTime;
    private int _hitHash;
    private bool _unlockHandled;

    public HitState(ActionStateMachine asm) : base(asm) { }

    public override void Enter()
    {
        _enterTime = Time.time;
        _hitHash = Animator.StringToHash("BeHit");
        _unlockHandled = false;
        // 不调用 base.Enter()：硬直期间不接收攻击输入（锁操作）
        Owner.Animator.CrossFadeInFixedTime("BeHit", 0.1f);   // 播受击动画
        Owner.PlayerAudio.PlayHurtVoice();
    }

    public override void Exit()
    {
        // 不调用 base.Exit()：本状态从未订阅输入，无需解绑
    }

    /// <summary>受击硬直是否锁定输入（动画前 70% 锁定；过渡期/非受击状态保守锁定）</summary>
    public bool IsLocked
    {
        get
        {
            if (Owner == null || Owner.Animator == null) return true;
            var info = Owner.Animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != _hitHash) return true;   // 过渡期/被打断，保守锁定
            return info.normalizedTime < LockReleaseTime;
        }
    }

    public override void Update()
    {
        // 兜底 1：动画事件没接上时强制恢复
        if (Time.time - _enterTime >= MaxDuration)
        {
            Asm.ChangeState(Asm.ActionNullState);
            return;
        }

        // 兜底 2：动画被其他过渡顶掉 → 提前恢复，不等动画退出信号
        if (Time.time - _enterTime > MinStayTime &&
            Owner.Animator.GetCurrentAnimatorStateInfo(0).shortNameHash != _hitHash)
        {
            Asm.ChangeState(Asm.ActionNullState);
            return;
        }

        // 后段放开（动画播到 70% 后）：若玩家还推着方向，自动切回移动，不用重新按键
        if (!IsLocked && !_unlockHandled)
        {
            _unlockHandled = true;
            if (Owner.MoveInput.MoveValue.magnitude > 0.1f)
                Owner.Locomotion.ChangeState(Owner.Locomotion.RunState);
        }
    }

    public override void OnAnimationExitEvent()
    {
        // 受击动画播完 → 恢复待机（可再输入/攻击）
        Asm.ChangeState(Asm.ActionNullState);
    }

    public override void OnAnimationTranslateEvent(IState newState)
    {
        // 硬直期间不被动画驱动的其他过渡抢走（如 HasInput 抢切）
    }
}
