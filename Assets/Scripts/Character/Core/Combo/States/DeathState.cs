using UnityEngine;

/// <summary>
/// 暂未启用：角色暂无死亡动画。等死亡动画做好后，
/// 在 ActionStateMachine 里重新挂回本状态，并在 PlayerController 死亡分支切到它即可。
/// </summary>
/// <summary>
/// 死亡状态 — 血量&lt;=0 时由 PlayerController 切入。
/// 播死亡动画（Animator 状态名 "Death"，由动画师配置），锁死全部操作；
/// 死亡不可被打断、不可退出（OnAnimationTranslateEvent / OnAnimationExitEvent 均忽略）。
/// </summary>
public class DeathState : PlayerComboState
{
    public DeathState(ActionStateMachine asm) : base(asm) { }

    public override void Enter()
    {
        // 不调用 base.Enter()：死亡期间不接收攻击输入
        Owner.Animator.CrossFadeInFixedTime("Death", 0.1f);
    }

    public override void Exit()
    {
        // 不调用 base.Exit()：从未订阅输入，无需解绑
    }

    public override void Update() { }

    public override void OnAnimationTranslateEvent(IState newState)
    {
        // 死亡动画不被其他过渡抢走
    }

    public override void OnAnimationExitEvent()
    {
        // 死亡动画播完留在本状态（尸体待机，等结算 UI）
    }
}
