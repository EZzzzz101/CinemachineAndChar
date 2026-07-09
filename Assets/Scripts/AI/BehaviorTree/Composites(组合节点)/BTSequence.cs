namespace AI.BehaviourTree
{
    [BTNode("顺序", "Composite", "从左到右依次执行，遇 Fail 即停，全 Success 才 Success")]
    public class BTSequence : BTComposite
    {
        protected override BTResult OnExecute(Blackboard bb)
        {
            for (int i = _runningIndex; i < Children.Count; i++)
            {
                BTResult result = Children[i].Execute(bb);

                if (result == BTResult.Failure)
                {
                    _runningIndex = 0;
                    return BTResult.Failure;
                }

                if (result == BTResult.Running)
                {
                    _runningIndex = i;
                    return BTResult.Running;
                }
            }

            _runningIndex = 0;
            return BTResult.Success;
        }
    }
}
