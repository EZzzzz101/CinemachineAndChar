using UnityEngine;

namespace AI.BehaviourTree
{
    /// <summary>
    /// 条件节点：检查 Blackboard 中是否存在 "target"
    /// 不需要配置参数 → 直接继承 BTCondition<object>（空数据占位）
    /// </summary>
    [BTNode("有目标?", "Condition/检测", "检查是否有锁定/追击目标")]
    public class BTHasTarget : BTCondition<object>
    {
        protected override BTResult OnExecute(Blackboard bb)
        {
            // 条件节点只做判断，立刻返回 Success 或 Failure
            // 永远不返回 Running
            return bb.Has("target")
                ? BTResult.Success
                : BTResult.Failure;
        }
    }
}
