using System.Collections.Generic;
using UnityEngine;

namespace AI.BehaviourTree
{
    /// <summary>
    /// 挂在 Animator 状态上的退出脚本（Animator 窗口 → 选中状态 → Add Behaviour）
    /// 按状态 hash 分键，多个动画各写各的信号，切换动画不会互相干扰：
    ///   OnStateEnter: 把状态 hash 加进黑板活跃集合（记录"当前在播什么"）
    ///   OnStateExit : 活跃集合里查得到才发该状态的完成信号（键 = 状态名 hash），并从集合移出
    /// 节点侧（如 BTDash.StateName）填状态短名，内部 Animator.StringToHash 转成同一个键。
    /// </summary>
    public class BTAnimationExitNotifier : StateMachineBehaviour
    {
        /// <summary>黑板里"当前活跃的动画状态"集合的键名</summary>
        public const string ActiveSetKey = "bt_active_anims";

        [Tooltip("退出时清掉的 Animator Bool 参数名（如 IsDashing）；留空 = 不清")]
        public string ClearBoolName = "";

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            Debug.Log($"[BT-Enter] {animator.name} hash={stateInfo.shortNameHash}");  // TODO 临时日志

            var runner = animator.GetComponent<BehaviorTreeRunner>();
            var bb = runner != null ? runner.Blackboard : null;
            if (bb == null) return;

            var set = bb.Get<HashSet<int>>(ActiveSetKey);
            if (set == null)
            {
                set = new HashSet<int>();
                bb.Set(ActiveSetKey, set);
            }
            set.Add(stateInfo.shortNameHash);
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            Debug.Log($"[BT-Exit] {animator.name} hash={stateInfo.shortNameHash}");  // TODO 临时日志

            // 1. 活跃集合里查得到才发信号（确保该状态确实进入过）；信号键 = 状态名 hash
            var runner = animator.GetComponent<BehaviorTreeRunner>();
            var bb = runner != null ? runner.Blackboard : null;
            if (bb != null)
            {
                var set = bb.Get<HashSet<int>>(ActiveSetKey);
                if (set != null && set.Remove(stateInfo.shortNameHash))
                    bb.Set(stateInfo.shortNameHash.ToString(), true);
            }

            // 2. 清掉 bool 驱动的状态参数（不依赖行为树，一定执行）
            if (!string.IsNullOrEmpty(ClearBoolName))
                animator.SetBool(ClearBoolName, false);
        }
    }
}
