using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct DeathData
    {
        [Tooltip("Animator 中驱动死亡的 Trigger 参数名，默认 \"Death\"")]
        public string TriggerName;

        [Tooltip("死亡动画状态短名（与 BTAnimationExitNotifier 匹配，动画退出时发完成信号），默认 \"Death\"")]
        public string StateName;

        [Tooltip("兜底超时(秒)：死亡动画退不出时强制完成，防卡死。0=不启用")]
        public float MaxDuration;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 动作节点：死亡（放在响应式根的最高优先级，由"血量&lt;=0"条件放行）
    ///   进入时：清移动参数 + 触发一次死亡 Trigger；
    ///   执行中：等死亡动画退出信号（BTAnimationExitNotifier）→ Success，超时兜底；
    ///   重入保护：黑板上 _dead 置位后（动画播放中/已完成）再被重评直接 Success，不重复触发。
    /// </summary>
    [BTNode("死亡", "Action/动画", "血量<=0 时触发：清移动参数、播死亡动画；动画播完返回 Success（带重入保护）")]
    public class BTDeath : BTAction<DeathData>
    {
        /// <summary>黑板死亡标记键：BTDeath 首次触发时置位，防止响应式根每 tick 重评重复触发</summary>
        public const string DeadKey = "_dead";

        private int _stateHash;
        private float _startTime;
        private bool _triggered;   // 本次是否真正触发过

        public override void OnEnter(Blackboard bb)
        {
            _triggered = false;
            _startTime = Time.time;
            _stateHash = Animator.StringToHash(string.IsNullOrEmpty(Data.StateName) ? "Death" : Data.StateName);

            // 重入保护：已经触发过死亡（动画播放中/已完成）→ 不再重复触发
            if (bb.Get<bool>(DeadKey)) return;

            Animator anim = bb.Get<Animator>("_animator");
            if (anim == null) return;

            _triggered = true;
            bb.Set(DeadKey, true);

            // 清掉 Motor 的旋转/移动标志：死亡后禁止面向玩家、禁止对峙移动
            bb.Set(BTSetFaceTarget.FaceTargetKey, false);
            bb.Set(BTStandoff.StandoffKey, 0);
            bb.Set(BTStandoff.TargetXKey, 0f);
            bb.Set(BTStandoff.TargetYKey, 0f);

            // 清移动参数，让动画干净地进入死亡状态
            anim.SetFloat("SpeedX", 0f);
            anim.SetFloat("SpeedY", 0f);
            anim.SetBool("IsMoving", false);
            anim.SetBool("IsSolo", false);

            anim.SetTrigger(string.IsNullOrEmpty(Data.TriggerName) ? "Death" : Data.TriggerName);
        }

        protected override BTResult OnExecute(Blackboard bb)
        {
            // 重入：已死过，直接完成
            if (!_triggered) return BTResult.Success;

            // 死亡动画退出信号（BTAnimationExitNotifier：状态名 hash → true）
            if (bb.Get<bool>(_stateHash.ToString()))
                return BTResult.Success;

            // 兜底：动画没接上/退不出时超时强制完成
            if (Data.MaxDuration > 0f && Time.time - _startTime >= Data.MaxDuration)
            {
                Debug.LogWarning($"[BT] BTDeath: 等待死亡动画退出超时({Data.MaxDuration:F1}s)，按完成处理");
                return BTResult.Success;
            }

            return BTResult.Running;
        }

        public override void OnExit(Blackboard bb)
        {
            _triggered = false;   // 实例状态复位；重入保护靠黑板 _dead
        }
    }
}
