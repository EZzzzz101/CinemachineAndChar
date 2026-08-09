using UnityEngine;

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

    public override void Update()
    {
        base.Update();   // 轮询攻击边沿 → OnFirePressed()
    }

    public override  void OnAnimationTranslateEvent(IState newState)
    {
        // 防循环：只有玩家主动按了攻击（_isEntering=true）才切 ATKState
        // 防止 Animator Controller 的过渡线意外触发 ATK 导致死循环
        if (!_isEntering) return;
        Asm.ChangeState(newState);
    }

    public override  void OnAnimationExitEvent() { }

    protected override void OnFirePressed()
    {
        if(_isEntering) return;

        _isEntering=true;

        // 锁定态：先面向敌人再闪身
        Owner.FaceEnemy();
        Owner.FlashToEnemy();

        ResuableData.comboIndex = 0;          // 起手重置段号
        ResuableData.currentATKIndex = 0;     // 起手重置击数
        ResuableData.canLinkCombo = true;     // 重置连招许可
        base.OnFirePressed();               // 播第一段动画
        Owner.PlayerAudio.PlayComboSound(ResuableData.CurrentStep.attackSound);
        Owner.PlayerAudio.PlayComboVoice(ResuableData.CurrentStep.voiceClips);
    }

}
