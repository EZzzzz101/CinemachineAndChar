using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct FaceTargetData
    {
        [Tooltip("旋转速度（度/秒），默认 720")]
        public float RotateSpeed;

        [Tooltip("角度差低于此值视为已面向（度），默认 5")]
        public float AngleThreshold;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 动作节点：平滑旋转面向目标
    /// 旋转过程中返回 Running，面向后返回 Success
    /// </summary>
    [BTNode("面向目标", "Action/移动", "平滑旋转面向黑板中存储的目标 Transform")]
    public class BTFaceTarget : BTAction<FaceTargetData>
    {
        private const float MinAngleThreshold = 0.5f;

        protected override BTResult OnExecute(Blackboard bb)
        {
            // 1. 取自己的 Transform（内置键 _transform）
            Transform self = bb.Get<Transform>("_transform");
            if (self == null)
                return BTResult.Failure;

            // 2. 从黑板读目标（固定键名 "target"）
            Transform target = bb.Get<Transform>("target");
            if (target == null)
                return BTResult.Failure;

            // 3. 算方向（忽略 Y 轴，只在水平面上转）
            Vector3 direction = target.position - self.position;
            direction.y = 0f;

            if (direction.magnitude < 0.01f)
                return BTResult.Success;

            // 4. Slerp 插值旋转 — speed/60 归一化：speed=60 每帧约转 1°，speed=720 每帧约转 12°
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            float speed = Data.RotateSpeed > 0f ? Data.RotateSpeed : 720f;
            float angle = Quaternion.Angle(self.rotation, targetRotation);

            float threshold = Data.AngleThreshold > MinAngleThreshold
                ? Data.AngleThreshold
                : 5f;

            if (angle <= threshold)
                return BTResult.Success;

            // 距离衰减：越近转越慢，避免玩家贴身时鬼畜
            float distFactor = Mathf.Min(direction.magnitude / 2f, 1f);
            float t = Mathf.Min(speed * Time.deltaTime / 30f * distFactor, 1f);
            self.rotation = Quaternion.Slerp(self.rotation, targetRotation, t);

            return BTResult.Running;
        }
    }
}
