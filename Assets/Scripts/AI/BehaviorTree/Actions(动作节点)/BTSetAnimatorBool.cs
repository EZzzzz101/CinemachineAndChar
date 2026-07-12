using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct AnimatorBoolData
    {
        [Tooltip("Animator Controller 中配置的 Bool 参数名")]
        public string ParameterName;

        [Tooltip("设置的值")]
        public bool Value;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 动作节点：设置 Animator 的 Bool 参数
    /// 用于控制持续状态（移动、防御等），一帧完成返回 Success
    /// </summary>
    [BTNode("设置动画Bool", "Action/动画", "设置 Animator Bool 参数，控制移动/防御等持续状态")]
    public class BTSetAnimatorBool : BTAction<AnimatorBoolData>
    {
        protected override BTResult OnExecute(Blackboard bb)
        {
            Animator anim = bb.Get<Animator>("_animator");
            if (anim == null)
            {
                Debug.LogWarning("[BT] BTSetAnimatorBool: 黑板中没有 _animator");
                return BTResult.Failure;
            }

            anim.SetBool(Data.ParameterName, Data.Value);
            return BTResult.Success;
        }
    }
}
