namespace AI.BehaviourTree
{
    /// <summary>
    /// 子树容器 — 运行时相当于一个分组外壳
    /// 执行逻辑：依次执行所有子节点，遇到 Failure 提前退出（类似 Sequence）
    /// 编辑器里：白色文件夹节点，双击进入子视图
    /// </summary>
    [BTNode(name: "文件夹", category: "Composite/文件夹", description: "将一段子树收拢为一个白色文件夹节点，双击进入子视图")]
    public class BTSubTree : BTComposite
    {
        protected override BTResult OnExecute(Blackboard bb)
        {
            // 依次执行所有子节点（同 Sequence 语义）
            for (int i = 0; i < Children.Count; i++)
            {
                // Resume from the last running child
                if (i < _runningIndex) continue;

                var result = Children[i].Execute(bb);
                if (result == BTResult.Running)
                {
                    _runningIndex = i;   // 记录断点，下次继续
                    return BTResult.Running;
                }

                _runningIndex = -1;

                if (result == BTResult.Failure)
                    return BTResult.Failure;
                // Success → 继续执行下一个
            }

            return BTResult.Success;
        }
    }
}
