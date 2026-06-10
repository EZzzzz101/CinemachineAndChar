
/// <summary>
/// 抽象状态机基类 — 所有状态机（Locomotion、Action）都继承它
/// ChangeState 的核心逻辑只写一次，子类复用
/// </summary>
public abstract class StateMachine
{
    private IState _currentState;
    public IState CurrentState => _currentState;

    public void ChangeState(IState newState)
    {
        if (newState == null) return;
        _currentState?.Exit();
        _currentState = newState;
        _currentState.Enter();
    }

    public void Update()
    {
        _currentState?.Update();
    }

    /// <summary>Animator 通过 Animation Event 通知切状态</summary>
    public void OnAnimationTranslateEvent(IState newState)
    {
        _currentState?.OnAnimationTranslateEvent(newState);
    }

    /// <summary>Animator 通过 Animation Event 通知动画结束</summary>
    public void OnAnimationExitEvent()
    {
        _currentState?.OnAnimationExitEvent();
    }
}