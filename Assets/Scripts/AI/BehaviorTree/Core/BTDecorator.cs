namespace AI.BehaviourTree
{
    /// <summary>
    /// 装饰节点基类 — 只有一个子节点，修改其行为或返回值
    /// </summary>
    public abstract class BTDecorator : BTNode
    {
        public BTNode Child;

        /// <summary>编辑器用：设置被装饰的子节点</summary>
        public void SetChild(BTNode child)
        {
            Child = child;
        }

        public override void ResetNode()
        {
            base.ResetNode();
            Child?.ResetNode();
        }
    }
}
