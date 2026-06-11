using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationExitBehaviour : StateMachineBehaviour
{

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator.TryGetComponent<PlayerController>(out var pc))
        {
            pc.OnAnimationExitEvent();  // 告诉 PlayerController
        }
    }
}
