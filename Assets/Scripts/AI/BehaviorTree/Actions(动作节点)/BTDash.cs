using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct DashData
    {
        [Tooltip("Animator Controller 中驱动冲刺的 Bool 参数名，如 IsDashing")]
        public string BoolName;

        [Tooltip("等待退出的 Animator 状态短名，如 \"Dash\"。内部转 hash 匹配 BTAnimationExitNotifier 的退出信号")]
        public string StateName;

        [Tooltip("兜底超时（秒）。超过该时间仍未等到动画退出信号则强制按完成处理，防止动画没接上时永久卡死。0 = 不启用")]
        public float MaxDuration;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 动作节点：冲刺（行为型动画）
    ///   OnEnter  : 清掉该状态上次残留的退出信号
    ///   执行     : 设置 Bool(true) 一次 → Running → 等待冲刺动画状态退出
    ///              （BTAnimationExitNotifier.OnStateExit 写信号）→ Success
    ///   OnExit   : 设置 Bool(false)（此时冲刺动画已结束才复位）
    ///
    /// 位移交给 Root Motion，行为树只负责"触发冲刺 + 等待冲刺动画结束"。
    /// MaxDuration 为兜底：动画没接上 / 退不出时，超时强制完成，防止永久卡死。
    /// </summary>
    [BTNode("冲刺", "Action/动画", "设置冲刺 Bool → 等待冲刺动画退出 → 复位 Bool（位移交给 Root Motion）")]
    public class BTDash : BTAction<DashData>
    {
        private bool _set;       // 本次进入是否已设置过 Bool
        private float _startTime;  // 进入时刻，用于超时兜底

        public override void OnEnter(Blackboard bb)
        {
            _set = false;
            _startTime = Time.time;
            // 清掉该状态上次残留的退出信号，避免刚进入就误判完成
            if (TryResolveSignalKey(out var key))
                bb.Set(key, false);
        }

        protected override BTResult OnExecute(Blackboard bb)
        {
            Animator anim = bb.Get<Animator>("_animator");
            if (anim == null)
            {
                Debug.LogWarning("[BT] BTDash: 黑板中没有 _animator");
                return BTResult.Failure;
            }

            // 只设置一次，等待动画退出期间不再重复设置
            if (!_set)
            {
                anim.SetBool(Data.BoolName, true);
                _set = true;
            }

            // 没填状态名 → 防止挂死，按完成处理
            if (!TryResolveSignalKey(out var signalKey))
            {
                Debug.LogWarning("[BT] BTDash: StateName 未填，按完成处理");
                return BTResult.Success;
            }

            // 动画已退出 → 完成
            if (bb.Get<bool>(signalKey))
                return BTResult.Success;

            // 兜底：动画没接上 / 退不出时，超过 MaxDuration 强制完成，避免永久卡死
            if (Data.MaxDuration > 0f && Time.time - _startTime >= Data.MaxDuration)
            {
                Debug.LogWarning($"[BT] BTDash: 等待 {Data.StateName} 退出超时({Data.MaxDuration:F1}s)，按完成处理");
                return BTResult.Success;
            }

            return BTResult.Running;
        }

        public override void OnExit(Blackboard bb)
        {
            // 冲刺结束（成功）才复位 Bool；未设置过（失败）不动 Animator
            if (!_set) return;

            Animator anim = bb.Get<Animator>("_animator");
            if (anim != null)
                anim.SetBool(Data.BoolName, false);
        }

        /// <summary>状态短名 → 信号键（= Animator.StringToHash，与退出脚本的 shortNameHash 一致）</summary>
        private bool TryResolveSignalKey(out string key)
        {
            if (string.IsNullOrEmpty(Data.StateName))
            {
                key = null;
                return false;
            }
            key = Animator.StringToHash(Data.StateName).ToString();
            return true;
        }
    }
}
