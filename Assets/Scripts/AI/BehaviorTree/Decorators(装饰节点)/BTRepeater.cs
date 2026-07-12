using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct RepeaterData
    {
        [Tooltip("重复次数，0 = 无限循环")]
        public int RepeatCount;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 装饰节点：重复执行子节点
    /// RepeatCount=0 无限循环，RepeatCount=N 执行 N 次后返回 Success
    /// 子节点 Running 时透传 Running
    /// </summary>
    [BTNode("重复执行", "Decorator/流程控制", "重复执行子节点指定次数，0=无限循环")]
    public class BTRepeater : BTDecorator<RepeaterData>
    {
        private int _currentCount;   // 已完成的次数

        public override void OnEnter(Blackboard bb)
        {
            _currentCount = 0;
        }

        protected override BTResult OnExecute(Blackboard bb)
        {
            if (Child == null)
                return BTResult.Failure;

            BTResult result = Child.Execute(bb);

            // 子节点还在跑 → 透传
            if (result == BTResult.Running)
                return BTResult.Running;

            // 子节点完成了（Success 或 Failure）
            // 无限循环模式（RepeatCount == 0）
            if (Data.RepeatCount <= 0)
            {
                Child.ResetNode();       // 重置子节点，下一 Tick 重新开始
                return BTResult.Running; // 对外永远 Running
            }

            // 有限次数模式
            _currentCount++;
            if (_currentCount >= Data.RepeatCount)
            {
                return BTResult.Success;  // 次数够了，完成
            }

            Child.ResetNode();
            return BTResult.Running;      // 还有剩余次数，继续跑
        }

        public override void ResetNode()
        {
            base.ResetNode();
            _currentCount = 0;
        }
    }
}
