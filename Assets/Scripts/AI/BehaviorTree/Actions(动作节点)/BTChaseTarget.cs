using UnityEngine;

namespace AI.BehaviourTree
{
    [System.Serializable]
    public struct ChaseTargetData
    {
        [Tooltip("旋转速度（度/秒），越大转得越快")]
        public float RotateSpeed;

        [Tooltip("Blend Tree 方向参数名（Animator float），-1左~1右")]
        public string DirectionParam;

        [Tooltip("移动触发参数名（Animator bool），对应 IsMoving")]
        public string MoveParam;

        [Tooltip("移动速度（米/秒），0 表示不移动仅转向")]
        public float MoveSpeed;

        [Tooltip("到达此距离内停止")]
        public float StopDistance;

        [Tooltip("角度差低于此值视为已面向（度）")]
        public float AngleThreshold;
    }

    /// <summary>
    /// 追击目标 — 目标从黑板 "target" 键读取
    ///
    /// 逻辑：
    ///   - 目标在正前方 → 追击（Blend Tree 方向混合），同时平滑转向
    ///   - 目标在身后 → 原地转身，不走
    ///   - 到达攻击范围 → Success
    /// </summary>
    [BTNode("追击目标", "Action/移动", "目标在前追，目标在后原地转身")]
    public class BTChaseTarget : BTAction<ChaseTargetData>
    {
        private Animator _animator;
        private float _lastTickTime;

        public override void OnEnter(Blackboard bb)
        {
            _lastTickTime = Time.time;
            _animator = bb.Get<Transform>("_transform")?.GetComponent<Animator>();
        }

        protected override BTResult OnExecute(Blackboard bb)
        {
            Transform self = bb.Get<Transform>("_transform");
            Transform target = bb.Get<Transform>("target");  // ← 固定从黑板读 target

            if (self == null || target == null)
                return BTResult.Failure;

            float dt = Time.time - _lastTickTime;
            _lastTickTime = Time.time;
            if (dt > 0.1f) dt = Time.deltaTime;

            Vector3 toTarget = target.position - self.position;
            toTarget.y = 0f;

            float dist = toTarget.magnitude;
            if (dist < 0.01f)
                return BTResult.Success;

            Vector3 dirToTarget = toTarget / dist;
            float forwardDot = Vector3.Dot(self.forward, dirToTarget);
            float rightDot = Vector3.Dot(self.right, dirToTarget);

            // ── 平滑旋转 ──
            float rotateSpeed = Data.RotateSpeed > 0f ? Data.RotateSpeed : 540f;
            Quaternion targetRot = Quaternion.LookRotation(dirToTarget);
            float angle = Quaternion.Angle(self.rotation, targetRot);
            float threshold = Data.AngleThreshold > 0.5f ? Data.AngleThreshold : 5f;

            if (angle > threshold)
            {
                float distFactor = Mathf.Min(dist / 2f, 1f);
                float t = Mathf.Min(rotateSpeed * dt / 60f * distFactor, 1f);
                self.rotation = Quaternion.Slerp(self.rotation, targetRot, t);
            }

            // ── Animator 参数 ──
            float stopDist = Data.StopDistance > 0f ? Data.StopDistance : 1.5f;
            bool isInFront = forwardDot > 0f;
            bool shouldMove = isInFront && dist > stopDist && Data.MoveSpeed > 0f;

            if (_animator != null)
            {
                string dirParam = string.IsNullOrEmpty(Data.DirectionParam) ? "Direction" : Data.DirectionParam;
                string moveParam = string.IsNullOrEmpty(Data.MoveParam) ? "IsMoving" : Data.MoveParam;

                foreach (AnimatorControllerParameter p in _animator.parameters)
                {
                    if (p.name == dirParam)
                        _animator.SetFloat(dirParam, isInFront ? rightDot : 0f);
                    if (p.name == moveParam)
                        _animator.SetBool(moveParam, shouldMove);
                }
            }

            // ── 位移（目标在前才走） ──
            if (shouldMove)
            {
                Vector3 move = dirToTarget * (Data.MoveSpeed * dt);
                self.position = new Vector3(
                    self.position.x + move.x,
                    self.position.y,
                    self.position.z + move.z
                );
            }

            // ── 到达判断 ──
            if (dist <= stopDist && angle <= threshold)
                return BTResult.Success;

            return BTResult.Running;
        }
    }
}
