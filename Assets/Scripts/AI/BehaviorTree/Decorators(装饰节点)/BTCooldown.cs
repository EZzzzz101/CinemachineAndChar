using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct CooldownData
    {
        [Tooltip("冷却时间（秒）")]
        public float CooldownTime;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 装饰节点：子节点成功后进入冷却，冷却期间直接返回 Failure
    /// 防止高频重复执行（如攻击每帧触发）
    /// </summary>
    [BTNode("冷却", "Decorator/流程控制", "子节点成功后冷却 N 秒，期间拒绝执行")]
    public class BTCooldown : BTDecorator<CooldownData>
    {
        private float _lastSuccessTime = float.MinValue;  // 上次子节点成功的时间戳

        public override void OnEnter(Blackboard bb)
        {
            // OnEnter 不做重置——冷却时间应该跨 Tick 保持
            // float.MinValue 意味着第一次一定放行
        }

        protected override BTResult OnExecute(Blackboard bb)
        {
            if (Child == null)
                return BTResult.Failure;

            // 冷却中 → 拒绝
            if (Time.time - _lastSuccessTime < Data.CooldownTime)
                return BTResult.Failure;

            // 放行 → 执行子节点
            BTResult result = Child.Execute(bb);

            // 子节点成功了 → 记录时间，开始新一轮冷却
            if (result == BTResult.Success)
                _lastSuccessTime = Time.time;

            return result;   // Running / Failure 原样返回
        }

        public override void ResetNode()
        {
            base.ResetNode();
            _lastSuccessTime = float.MinValue;
        }
    }
}
