using System.Collections.Generic;
using UnityEngine;

namespace AI.BehaviourTree
{
    /// <summary>
    /// 加权随机数据
    /// </summary>
    [System.Serializable]
    public class WeightedRandomData
    {
        [System.Serializable]
        public class WeightEntry
        {
            [Tooltip("对应子节点的名字")]
            public string Label;
            [Tooltip("权重百分比 0~100，0=自动计算（最后一个0自动补全到100）")]
            public float Weight;
        }

        public List<WeightEntry> Entries = new();
    }

    /// <summary>
    /// 组合节点：加权随机选一个子节点执行
    ///
    /// 用法：每个子节点配一个百分比，最后一项设 0 自动计算（100 - 前面之和）
    /// Weight=0 表示自动计算
    /// </summary>
    [BTNode("加权随机", "Composite", "按百分比随机选一个子节点执行，Weight=0自动计算")]
    public class BTWeightedRandom : BTComposite<WeightedRandomData>
    {
        private int _selectedIndex = -1;

        // ===== 逻辑 =====
        protected override BTResult OnExecute(Blackboard bb)
        {
            if (Children.Count == 0)
                return BTResult.Failure;

            // 还没选 → 按权重抽一个
            if (_selectedIndex < 0 || _selectedIndex >= Children.Count)
            {
                _selectedIndex = PickWeightedRandom();
            }

            BTResult result = Children[_selectedIndex].Execute(bb);

            // 子节点执行完了 → 重置，下次重新抽
            if (result != BTResult.Running)
            {
                _selectedIndex = -1;
            }

            return result;
        }

        private int PickWeightedRandom()
        {
            var entries = Data.Entries;

            // 1. 解析百分比，最后一个 0 自动补全
            float[] percentages = new float[Children.Count];
            float sum = 0f;
            int autoIndex = -1;

            for (int i = 0; i < Children.Count; i++)
            {
                if (entries != null && i < entries.Count && entries[i].Weight > 0f)
                {
                    percentages[i] = entries[i].Weight;
                    sum += percentages[i];
                }
                else
                {
                    autoIndex = i;  // 找到最后一个 0 的作为自动计算位
                }
            }

            // 如果有自动位，补全到 100
            if (autoIndex >= 0 && sum < 100f)
            {
                percentages[autoIndex] = Mathf.Max(0f, 100f - sum);
                sum = 100f;
            }

            if (sum <= 0f)
                return Random.Range(0, Children.Count);

            // 2. 随机抽取
            float r = Random.Range(0f, sum);
            float cumulative = 0f;

            for (int i = 0; i < Children.Count; i++)
            {
                cumulative += percentages[i];
                if (r < cumulative)
                    return i;
            }

            return Children.Count - 1;
        }

        public override void ResetNode()
        {
            base.ResetNode();
            _selectedIndex = -1;
        }
    }
}
