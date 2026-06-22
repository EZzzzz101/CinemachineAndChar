using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 动作空状态 — 没在攻击时待在这里，接收 Fire 输入进入 Combo
/// </summary>
public class ActionNullState : PlayerComboState
{
    private bool _isEntering;//是否进入下个状态
    public ActionNullState(ActionStateMachine Asm):base(Asm){}

    public override void Enter()
    {
        base.Enter();
        _isEntering=false;
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
        if(_isEntering) return;

        _isEntering=true;
        ResuableData.comboIndex = 0;    // ← 重置
        base.OnFireStarted(ctx);        // 播第一段动画
        CharacterAudio.Instance.PlayComboSound(ResuableData.CurrentStep.attackSound);
    }

}
