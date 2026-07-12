using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========

    /// <summary>比较运算符</summary>
    public enum CompareOp
    {
        Greater,        // >   黑板值 > 阈值
        Less,           // <
        GreaterEqual,   // >=
        LessEqual,      // <=
        Equal,          // ==
        NotEqual        // !=
    }

    [System.Serializable]
    public struct BlackboardConditionData
    {
        [Tooltip("黑板中要检查的键名")]
        public string Key;

        [Tooltip("比较运算符")]
        public CompareOp Op;

        [Tooltip("比较的阈值")]
        public float Value;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 条件节点：比较黑板中某个数值与阈值的关系
    /// 适用于血量检测、距离判断、任意数值条件
    /// </summary>
    [BTNode("数值条件", "Condition/通用", "比较黑板数值与阈值：> < >= <= == !=")]
    public class BTBlackboardCondition : BTCondition<BlackboardConditionData>
    {
        protected override BTResult OnExecute(Blackboard bb)
        {
            // 1. 取黑板值
            if (!bb.Has(Data.Key))
                return BTResult.Failure;

            float blackboardValue = bb.Get<float>(Data.Key);

            // 2. 根据运算符比较
            bool result = Data.Op switch
            {
                CompareOp.Greater       => blackboardValue >  Data.Value,
                CompareOp.Less          => blackboardValue <  Data.Value,
                CompareOp.GreaterEqual  => blackboardValue >= Data.Value,
                CompareOp.LessEqual     => blackboardValue <= Data.Value,
                CompareOp.Equal         => Mathf.Approximately(blackboardValue, Data.Value),
                CompareOp.NotEqual      => !Mathf.Approximately(blackboardValue, Data.Value),
                _                       => false
            };

            return result ? BTResult.Success : BTResult.Failure;
        }
    }
}
