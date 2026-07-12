using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct AnimationDoneData
    {
        [Tooltip("Animator 中的状态名，如 \"Attack\"、\"HitReact\"")]
        public string StateName;

        [Tooltip("动画层索引，默认 0 = Base Layer")]
        public int Layer;

        [Tooltip("进度阈值 0~1，默认 0.95 表示播到 95%")]
        public float Threshold;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 条件节点：检查指定动画是否播放到指定进度
    /// </summary>
    [BTNode("动画播完了?", "Condition/动画", "检查 Animator 当前状态是否为指定动画，且进度 >= 阈值")]
    public class BTIsAnimationDone : BTCondition<AnimationDoneData>
    {
        protected override BTResult OnExecute(Blackboard bb)
        {
            // 1. 从黑板拿 Animator（_animator 是构造 Blackboard 时自动绑定的）
            Animator anim = bb.Get<Animator>("_animator");
            if (anim == null)
                return BTResult.Failure;

            // 2. 获取当前层的动画状态信息
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(Data.Layer);

            // 3. 先检查是不是目标动画（不是 → 还没切过来，不算完成）
            if (!stateInfo.IsName(Data.StateName))
                return BTResult.Failure;

            // 4. 检查进度（>= 阈值就算完成）
            //    normalizedTime: 0=开始, 0.5=一半, 1=播完, >1=循环中
            float threshold = Data.Threshold > 0f ? Data.Threshold : 0.95f;
            return stateInfo.normalizedTime >= threshold
                ? BTResult.Success
                : BTResult.Failure;
        }
    }
}
