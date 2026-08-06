namespace AI.BehaviourTree
{
    /// <summary>
    /// 响应式选择器 — 每 tick 从 index 0 全量重评（不做断点续跑）。
    /// 高优先级分支本轮进入 Running/Success 时，抢占并递归中止低优先级 Running 子树。
    /// 用于"攻击随时打断对峙"这类兜底逻辑：低优先级分支只是兜底，高优先级条件一满足立刻切过去。
    ///
    /// 注意：分支 Success 会被吸收为 Running 持有（不向上冒泡），
    /// 让父级保持 Running，下一 tick 从 0 重评（如：攻击播完→冷却中→自然落对峙）。
    /// 只有全部分支都 Failure（如丢目标）才向上冒泡 Failure。
    /// </summary>
    [BTNode("响应式选择", "Composite",
        "每 tick 从 index0 全量评估；高优先级分支就绪时中止低优先级 Running 子树")]
    public class BTReactiveSelector : BTComposite
    {
        protected override BTResult OnExecute(Blackboard bb)
        {
            int previous = _runningIndex;   // 上一轮活跃分支
            for (int i = 0; i < Children.Count; i++)   // 永远从 0 全量扫描，不依赖断点
            {
                BTResult result = Children[i].Execute(bb);

                if (result == BTResult.Running)
                {
                    // 高优先级分支已开始执行：旧的 Running 分支还挂着 → 递归中止（自底向上 OnExit + ResetNode）
                    if (previous >= 0 && previous != i
                        && previous < Children.Count && Children[previous].IsRunning)
                        Children[previous].Abort(bb);
                    _runningIndex = i;
                    return BTResult.Running;
                }

                if (result == BTResult.Success)
                {
                    // 分支本轮完成（如攻击播完）：同样抢占旧 Running（防御），
                    // 吸收为 Running 持有，不向上冒泡；下轮从 0 重评条件
                    if (previous >= 0 && previous != i
                        && previous < Children.Count && Children[previous].IsRunning)
                        Children[previous].Abort(bb);
                    _runningIndex = i;
                    return BTResult.Running;
                }
                // Failure → 继续试下一个
            }

            _runningIndex = -1;
            return BTResult.Failure;   // 全分支不可用（如丢目标）→ 向上冒泡
        }

        public override void ResetNode()
        {
            base.ResetNode();   // 递归重置子节点 + _runningIndex=0
            _runningIndex = -1;
        }
    }
}
