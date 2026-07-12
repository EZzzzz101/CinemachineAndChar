using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct AnimatorTriggerData
    {
        [Tooltip("Animator Controller 中配置的 Trigger 参数名")]
        public string TriggerName;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 动作节点：设置 Animator 的 Trigger 参数
    /// 瞬时操作，一帧完成返回 Success
    /// </summary>
    [BTNode("设置动画Trigger", "Action/动画", "设置 Animator Trigger 参数，用于触发攻击/受击等动画")]
    public class BTSetAnimatorTrigger : BTAction<AnimatorTriggerData>
    {
        protected override BTResult OnExecute(Blackboard bb)
        {
            Animator anim = bb.Get<Animator>("_animator");
            if (anim == null)
            {
                Debug.LogWarning("[BT] BTSetAnimatorTrigger: 黑板中没有 _animator");
                return BTResult.Failure;
            }

            if (string.IsNullOrEmpty(Data.TriggerName))
            {
                Debug.LogWarning("[BT] BTSetAnimatorTrigger: TriggerName 为空");
                return BTResult.Failure;
            }

            anim.SetTrigger(Data.TriggerName);
            return BTResult.Success;  // 瞬时完成
        }
    }
}
