using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct SetBlackboardData
    {
        [Tooltip("黑板中已定义的键名")]
        public string Key;

        [Tooltip("写入的值")]
        public float FloatValue;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 动作节点：向黑板写入一个 float 值
    /// 放在条件节点后面用来"记录决策结果"，供外部 Controller 读取
    /// 一帧完成，不跨帧
    /// </summary>
    [BTNode(name: "设黑板值", category: "Action/数据",
        description: "向黑板写入一个 float 值（一帧完成）")]
    public class BTSetBlackboard : BTAction<SetBlackboardData>
    {
        protected override BTResult OnExecute(Blackboard bb)
        {
            if (string.IsNullOrEmpty(Data.Key))
            {
                Debug.LogWarning("[BT] BTSetBlackboard: Key 为空");
                return BTResult.Failure;
            }

            bb.Set(Data.Key, Data.FloatValue);
            return BTResult.Success;
        }
    }
}
