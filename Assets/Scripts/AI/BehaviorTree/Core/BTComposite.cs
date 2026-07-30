using System.Collections.Generic;
using UnityEngine;

namespace AI.BehaviourTree
{
    /// <summary>
    /// 组合节点基类 — 有多个子节点，决定按什么顺序 / 逻辑执行
    /// </summary>
    public abstract class BTComposite : BTNode
    {
        public List<BTNode> Children = new List<BTNode>();
        protected int _runningIndex;  // 记住上一帧哪个子节点在 Running

        /// <summary>编辑器用：建立父子关系</summary>
        public void AddChild(BTNode child)
        {
            Children.Add(child);
        }

        /// <summary>编辑器用：移除子节点</summary>
        public void RemoveChild(BTNode child)
        {
            Children.Remove(child);
        }

        /// <summary>清空子节点（重新连线时用）</summary>
        public void ClearChildren()
        {
            Children.Clear();
        }

        public override void ResetNode()
        {
            base.ResetNode();
            _runningIndex = 0;
            // 递归重置所有子节点
            foreach (var child in Children)
                child.ResetNode();
        }
    }

    /// <summary>
    /// 泛型组合节点基类 — 有配置参数的组合节点继承这个
    /// 带 T Data + 自动序列化/反序列化
    /// </summary>
    public abstract class BTComposite<T> : BTComposite where T : new()
    {
        public T Data = new T();

        public string SerializeData() =>
            JsonUtility.ToJson(Data);

        public void DeserializeData(string json)
        {
            if (!string.IsNullOrEmpty(json))
                Data = JsonUtility.FromJson<T>(json) ?? new T();
        }
    }
}
