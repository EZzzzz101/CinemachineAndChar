namespace AI.BehaviourTree
{
    /// <summary>
    /// 取反：子节点 Success → Failure，Failure → Success，Running 透传
    /// </summary>
    [BTNode("取反", "Decorator", "反转子节点结果")]
    public class BTInverter : BTDecorator
    {
        protected override BTResult OnExecute(Blackboard bb)
        {
            if (Child == null)
                return BTResult.Failure;

            BTResult result = Child.Execute(bb);

            return result switch
            {
                BTResult.Success => BTResult.Failure,
                BTResult.Failure => BTResult.Success,
                _ => BTResult.Running
            };
        }
    }
}
