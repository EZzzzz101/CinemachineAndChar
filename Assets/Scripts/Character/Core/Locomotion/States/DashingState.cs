using UnityEngine;

public class DashingState : LocomotionState
{
    private float _enterTime;
    private bool _invincibleActive;

    public DashingState (LocomotionStateMachine sm) : base(sm) { }

    public override void Enter()
    {
        base.Enter();
        _enterTime = Time.time;
        _invincibleActive = true;
        Owner.IsInvincible = true;      // 闪避无敌帧开启：受击免疫
        Owner.PlayerAudio.PlayDodgeSound(Owner.LastAnimEnterState);
        Debug.Log($"[DashingState] Enter via: {Owner.LastAnimEnterState}");
    }

    public override void Update()
    {
        base.Update();

        // 无敌帧窗口结束 → 关闭免疫
        if (_invincibleActive && Time.time - _enterTime >= Owner.DodgeInvincibleTime)
        {
            _invincibleActive = false;
            Owner.IsInvincible = false;
        }
    }

    #region Dash转到 Idle?Sprint
    public override void OnAnimationExitEvent()
    {
        Owner.IsInvincible = false;     // 防御性关闭
        if (Owner.MoveInput.MoveValue.magnitude <0.1f)
        {
            Sm.ChangeState(Sm.IdleState);
            return;
        }
        Sm.ChangeState(Sm.SprintState);
    }
    #endregion

    public override void Exit()
    {
        Owner.IsInvincible = false;     // 防御性关闭（任何方式离开冲刺都失效）
        base.Exit();
    }
}
