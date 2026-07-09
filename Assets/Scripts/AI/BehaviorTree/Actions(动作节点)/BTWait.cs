using UnityEngine;

namespace AI.BehaviourTree
{
    [System.Serializable]
    public struct WaitData
    {
        public float Duration;
    }

    /// <summary>
    /// 等待指定秒数，期间返回 Running，时间到返回 Success
    /// </summary>
    [BTNode("等待", "Action/时间", "等待指定秒数")]
    public class BTWait : BTAction<WaitData>
    {
         private float _startTime;

        public override void OnEnter(Blackboard bb)
        {
            _startTime = Time.time;
        }

        protected override BTResult OnExecute(Blackboard bb)
        {
            return Time.time - _startTime >= Data.Duration
                ? BTResult.Success
                : BTResult.Running;
        }
    }
}
