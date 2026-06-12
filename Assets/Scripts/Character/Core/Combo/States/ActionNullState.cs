using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 动作空状态 — 没在攻击时待在这里，接收 Fire 输入进入 Combo
/// </summary>
public class ActionNullState : IState
{
    protected readonly ActionStateMachine Sm;
    protected readonly PlayerController Owner;

    public ActionNullState(ActionStateMachine sm)
    {
        Sm = sm;
        Owner = sm.Owner;
    }

    public void Enter()
    {
        Owner.PlayerInput.actions["Player/Fire"].started += OnFireStarted;
    }

    public void Exit()
    {
        Owner.PlayerInput.actions["Player/Fire"].started -= OnFireStarted;
    }

    public void Update() { }

    public void OnAnimationTranslateEvent(IState newState)
    {
        Sm.ChangeState(newState);  // 动画事件：切到 Combo
    }

    public void OnAnimationExitEvent() { }

    private void OnFireStarted(InputAction.CallbackContext ctx)
    {
        Owner.Animator.CrossFadeInFixedTime("Anbi_Normal_1", 0.111f);
    }
}
