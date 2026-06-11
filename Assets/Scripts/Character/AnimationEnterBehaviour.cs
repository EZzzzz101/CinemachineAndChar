// 挂在动画 clip 的 Inspector 上
using UnityEngine;

public class AnimationEnterBehaviour : StateMachineBehaviour
{
    [SerializeField] private string _targetState; // 在Inspector里填 "DashingState"

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.TryGetComponent<PlayerController>(out var pc))
        {
            pc.OnAnimationTranslateEvent(_targetState);  // 告诉 PlayerController
        }
    }
}
