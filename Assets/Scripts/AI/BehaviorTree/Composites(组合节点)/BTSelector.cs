namespace AI.BehaviourTree
{
    [BTNode("选择", "Composite", "从左到右依次尝试，遇 Success 即停，全 Failure 才 Failure")]
    public class BTSelector : BTComposite
    {
        protected override BTResult OnExecute(Blackboard bb)
        {
            for (int i = _runningIndex; i < Children.Count; i++)
            {
                BTResult result = Children[i].Execute(bb);

                if (result == BTResult.Success)
                {
                    _runningIndex = 0;
                    return BTResult.Success;
                }

                if (result == BTResult.Running)
                {
                    _runningIndex = i;
                    return BTResult.Running;
                }
            }

            _runningIndex = 0;
            return BTResult.Failure;
        }
    }
}
