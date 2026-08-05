using System.Collections.Generic;
using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct HitReactionData
    {
        [Tooltip("等待退出的 Animator 状态短名，默认 \"Hit\"。内部转 hash 匹配 BTAnimationExitNotifier 的活跃集合")]
        public string StateName;

        [Tooltip("兜底超时(秒)：受击动画退不出时强制完成，防卡死。0=不启用")]
        public float MaxDuration;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 动作节点：受击反应（高优先级打断）
    ///   受击动画(Hit)正在播 → Running 占住整棵树(真硬直，其他分支不被 tick)
    ///   受击动画退出 → Success，恢复正常行为
    ///   触发动画由 BossBrain.TakeDamage 的 SetTrigger("Hit") + AnyState→Hit 完成，本节点只负责"等它播完"。
    ///   多段连击连续命中时 Hit 重入，活跃集合持续含 Hit → 持续硬直，不会提前结束（连击硬直锁）。
    /// </summary>
    [BTNode("受击反应", "Action/动画", "等受击动画(Hit)播完：播着就占住树(硬直)，退出后恢复")]
    public class BTHitReaction : BTAction<HitReactionData>
    {
        private int _stateHash;
        private float _startTime;
        private bool _engaged;   // 是否已确认受击动画活跃过

        public override void OnEnter(Blackboard bb)
        {
            string name = string.IsNullOrEmpty(Data.StateName) ? "Hit" : Data.StateName;
            _stateHash = Animator.StringToHash(name);
            _startTime = Time.time;
            _engaged = false;
        }

        protected override BTResult OnExecute(Blackboard bb)
        {
            var set = bb.Get<HashSet<int>>(BTAnimationExitNotifier.ActiveSetKey);
            bool active = set != null && set.Contains(_stateHash);

            if (!_engaged)
            {
                // 没在受击 → 让位给后续分支（快速 Failure）
                if (!active) return BTResult.Failure;
                _engaged = true;
            }

            // 受击动画已退出 → 恢复
            if (!active) return BTResult.Success;

            // 兜底：动画没接上/退不出时超时强制完成
            if (Data.MaxDuration > 0f && Time.time - _startTime >= Data.MaxDuration)
            {
                Debug.LogWarning($"[BT] BTHitReaction: 等待受击动画退出超时({Data.MaxDuration:F1}s)，按完成处理");
                return BTResult.Success;
            }

            return BTResult.Running;
        }

        public override void OnExit(Blackboard bb)
        {
            // 防御性复位：被打断的分支自己会复位(BTStandoff/BTDash.OnExit)，这里兜一层
            Animator anim = bb.Get<Animator>("_animator");
            if (anim == null) return;
            anim.SetFloat("SpeedX", 0f);
            anim.SetFloat("SpeedY", 0f);
        }
    }
}
