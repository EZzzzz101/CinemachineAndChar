// 挂在动画 clip 的 Inspector 上
using UnityEngine;

public class AnimationEnterBehaviour : StateMachineBehaviour
{
    public enum AnimationEnterState
    {
        DashFront,
        DashBack,

        Atk,
    }
    [SerializeField] private AnimationEnterState _targetState;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.TryGetComponent<PlayerController>(out var pc))
        {
            pc.OnAnimationTranslateEvent(_targetState);  // 告诉 PlayerController
        }
    }
}
