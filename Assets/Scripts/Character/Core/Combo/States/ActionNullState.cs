using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 动作空状态 — 没在攻击时待在这里，接收 Fire 输入进入 Combo
/// </summary>
public class ActionNullState : PlayerComboState
{

    public ActionNullState(ActionStateMachine Asm):base(Asm){}

    public override void Enter()
    {
        base.Enter();
    }

    public override  void Exit()
    {
        base.Exit();
    }

    public override  void Update() { }

    public override  void OnAnimationTranslateEvent(IState newState)
    {
        Asm.ChangeState(newState);  // 动画事件：切到 ATKState
    }

    public override  void OnAnimationExitEvent() { }

    protected override void OnFireStarted(InputAction.CallbackContext ctx)
    {
        ResuableData.comboIndex = 0;    // ← 重置
        base.OnFireStarted(ctx);        // 播第一段
    }

}
