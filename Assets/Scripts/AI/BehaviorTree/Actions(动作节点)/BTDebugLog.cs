using UnityEngine;

namespace AI.BehaviourTree
{
    [BTNode("打印日志", "Action/调试", "调试用：在控制台输出一段文字")]
    [System.Serializable]
    public struct DebugLogData
    {
        public string Message;
    }

    public class BTDebugLog : BTAction<DebugLogData>
    {
        protected override BTResult OnExecute(Blackboard bb)
        {
            Debug.Log($"[BT] {Data.Message}");
            return BTResult.Success;
        }
    }
}
