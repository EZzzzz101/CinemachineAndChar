namespace AI.BehaviourTree
{
    /// <summary>
    /// 装饰节点：强制成功
    /// 无论子节点返回 Success 还是 Failure，都向上返回 Success
    /// 子节点 Running 时透传 Running
    /// </summary>
    [BTNode("强制成功", "Decorator/结果控制", "忽略子节点的失败，始终向上返回成功")]
    public class BTSucceeder : BTDecorator
    {
        protected override BTResult OnExecute(Blackboard bb)
        {
            if (Child == null)
                return BTResult.Success;    // 没孩子也算成功

            BTResult result = Child.Execute(bb);

            // Running 透传，其他全部改成 Success
            return result == BTResult.Running
                ? BTResult.Running
                : BTResult.Success;
        }
    }
}
