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
    /// 动作节点：设置 Animator 的 Bool 参数（状态型参数，如 IsMoving / IsGuard / IsCharging）
    /// 设置一次 → 一帧完成返回 Success。持续状态的生命周期由节点进入/离开保证。
    ///
    /// 行为型动画（触发 → 等待动画结束 → 完成）请用专用节点，如 <see cref="BTDash"/>（冲刺）。
    /// </summary>
    [BTNode("设置动画Bool", "Action/动画", "设置 Animator Bool 参数（状态型），一帧完成；行为动画请用 BTDash 等专用节点")]
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
