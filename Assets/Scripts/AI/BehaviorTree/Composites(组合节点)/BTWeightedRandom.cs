using UnityEngine;

namespace AI.BehaviourTree
{
    /// <summary>
    /// 组合节点：加权随机选一个子节点执行
    ///
    /// 用法：每个子节点对应一个权重，权重越高被选中的概率越大
    /// 选中的子节点执行完后返回其结果（Success/Failure）
    ///
    /// JSON 格式: {"Weights":[1,3,5,2]}
    /// 数组顺序对应 Children 列表顺序
    /// </summary>
    [BTNode("加权随机", "Composite", "按权重随机选一个子节点执行")]
    public class BTWeightedRandom : BTComposite
    {
        public float[] Weights = System.Array.Empty<float>();

        private int _selectedIndex = -1;

        // ===== 序列化 =====
        public string SerializeData()
        {
            return JsonUtility.ToJson(new WeightData { Weights = Weights });
        }

        public void DeserializeData(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            var data = JsonUtility.FromJson<WeightData>(json);
            if (data.Weights != null && data.Weights.Length > 0)
                Weights = data.Weights;
        }

        [System.Serializable]
        private struct WeightData
        {
            public float[] Weights;
        }

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
            if (Weights == null || Weights.Length == 0)
            {
                // 没设权重 → 平均随机
                return Random.Range(0, Children.Count);
            }

            // 权重数量不够 → 剩下的平均分
            float total = 0f;
            int count = Mathf.Min(Weights.Length, Children.Count);
            for (int i = 0; i < count; i++)
                total += Mathf.Max(0f, Weights[i]);

            // 剩余子节点给默认权重 1
            total += Mathf.Max(0, Children.Count - count);

            float r = Random.Range(0f, total);
            float cumulative = 0f;

            for (int i = 0; i < Children.Count; i++)
            {
                float w = (i < Weights.Length) ? Mathf.Max(0f, Weights[i]) : 1f;
                cumulative += w;
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
