using System.Collections.Generic;

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
}
