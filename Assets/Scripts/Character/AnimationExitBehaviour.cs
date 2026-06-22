using UnityEngine;

public class AnimationExitBehaviour : StateMachineBehaviour
{
    public enum AnimExitState
    {
        Dash,
        Atk,
    }

    [SerializeField] private AnimExitState _exitState;

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.TryGetComponent<PlayerController>(out var pc))
        {
            pc.OnAnimationExitEvent(_exitState);
        }
    }
}
