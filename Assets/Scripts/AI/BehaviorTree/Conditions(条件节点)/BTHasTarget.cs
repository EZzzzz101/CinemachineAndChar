using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct HasTargetData
    {
        [Tooltip("要检查的黑板键名，默认 target")]
        public string TargetKey;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 条件节点：检查 Blackboard 中是否存在指定键
    /// </summary>
    [BTNode("有目标?", "Condition/检测", "检查黑板中是否存在指定键（默认 target）")]
    public class BTHasTarget : BTCondition<HasTargetData>
    {
        protected override BTResult OnExecute(Blackboard bb)
        {
            string key = string.IsNullOrEmpty(Data.TargetKey)
                ? "target"
                : Data.TargetKey;

            return bb.Has(key)
                ? BTResult.Success
                : BTResult.Failure;
        }
    }
}
