/// <summary>
/// 状态接口 — 所有状态必须实现的方法
/// </summary>
public interface IState
{
    void Enter();
    void Exit();
    void Update();
    void OnAnimationTranslateEvent(IState newState); // Animator → C#：切状态
    void OnAnimationExitEvent(); // Animator → C#：动画结束
}
