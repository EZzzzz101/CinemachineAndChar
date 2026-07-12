using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    // 决定 BehaviorTreeSO.NodeEntry.JsonData 里存什么
    // 例如: {"Range":2.0,"TargetKey":"target"}
    [System.Serializable]
    public struct RangeData
    {
        [Tooltip("检测半径（米）")]
        public float Range;

        [Tooltip("黑板中目标 Transform 的键名")]
        public string TargetKey;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 条件节点：检查目标是否在指定范围内
    /// </summary>
    [BTNode("目标在范围内?", "Condition/检测", "检测目标与自身的距离是否 <= 配置的阈值")]
    public class BTIsTargetInRange : BTCondition<RangeData>
    {
        protected override BTResult OnExecute(Blackboard bb)
        {
            // 1. 从黑板取目标 Transform
            //    如果 TargetKey 为空，默认用 "target"
            string key = string.IsNullOrEmpty(Data.TargetKey)
                ? "target"
                : Data.TargetKey;

            if (!bb.Has(key))
                return BTResult.Failure;   // 没有目标 → 失败

            Transform target = bb.Get<Transform>(key);
            if (target == null)
                return BTResult.Failure;   // Transform 被销毁了 → 失败

            // 2. 从黑板取自己的 Transform
            Transform self = bb.Get<Transform>("_transform");
            if (self == null)
                return BTResult.Failure;   // 安全检查

            // 3. 计算距离
            float distance = Vector3.Distance(self.position, target.position);

            // 4. 条件判断 → 只返回 Success 或 Failure
            return distance <= Data.Range
                ? BTResult.Success
                : BTResult.Failure;
        }
    }
}
