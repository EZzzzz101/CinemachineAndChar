using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct MoveTowardsData
    {
        [Tooltip("移动速度（米/秒）")]
        public float Speed;

        [Tooltip("黑板中目标 Transform 的键名")]
        public string TargetKey;

        [Tooltip("到达此距离内停止移动")]
        public float StopDistance;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 动作节点：向目标移动（Transform 直接位移，无寻路）
    /// 到达停止距离返回 Success，未到达返回 Running
    /// </summary>
    [BTNode("走向目标", "Action/移动", "向黑板中的目标 Transform 直线移动，到达后返回 Success")]
    public class BTMoveTowards : BTAction<MoveTowardsData>
    {
        private float _lastTickTime;  // 上次 Tick 的时间戳

        public override void OnEnter(Blackboard bb)
        {
            _lastTickTime = Time.time;
            Transform target = GetTarget(bb);
            float dist = target != null
                ? Vector3.Distance(bb.Get<Transform>("_transform").position, target.position)
                : 0f;
            Debug.Log($"[BT] BTMoveTowards: 开始走向 {target?.name}, 距离 {dist:F1}m");
        }

        protected override BTResult OnExecute(Blackboard bb)
        {
            Transform self = bb.Get<Transform>("_transform");
            Transform target = GetTarget(bb);

            if (self == null || target == null)
                return BTResult.Failure;

            float speed = Data.Speed > 0f ? Data.Speed : 3f;
            float stopDist = Data.StopDistance > 0f ? Data.StopDistance : 1.5f;

            Vector3 toTarget = target.position - self.position;
            toTarget.y = 0f;

            if (toTarget.magnitude <= stopDist)
            {
                Debug.Log($"[BT] BTMoveTowards: 到达目标 {target.name}");
                return BTResult.Success;
            }

            // 用两次 Tick 之间的实际时间间隔，不依赖 Time.deltaTime
            float dt = Time.time - _lastTickTime;
            _lastTickTime = Time.time;

            // 只在 XZ 平面移动，保持自己的 Y 不变
            Vector3 targetXZ = new Vector3(target.position.x, self.position.y, target.position.z);
            self.position = Vector3.MoveTowards(
                self.position,
                targetXZ,
                speed * dt
            );

            return BTResult.Running;
        }

        private Transform GetTarget(Blackboard bb)
        {
            string key = string.IsNullOrEmpty(Data.TargetKey) ? "target" : Data.TargetKey;
            return bb.Get<Transform>(key);
        }
    }
}
