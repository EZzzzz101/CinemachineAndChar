using UnityEngine;

namespace AI.BehaviourTree
{
    // ========== 数据层 ==========
    [System.Serializable]
    public struct FindTargetData
    {
        [Tooltip("搜索半径（米）")]
        public float MaxRange;

        [Tooltip("只搜索这个阵营的目标")]
        public Team TargetTeam;

        [Tooltip("找到后写入黑板的键名，默认 target")]
        public string TargetKey;
    }

    // ========== 逻辑层 ==========
    /// <summary>
    /// 动作节点：通过 TargetManager 在范围内找最近的指定阵营目标，写入黑板
    /// 找到返回 Success，没找到返回 Failure
    /// </summary>
    [BTNode("寻找目标", "Action/检测", "在范围内搜索最近的目标（按阵营过滤）并写入黑板")]
    public class BTFindNearestTarget : BTAction<FindTargetData>
    {
        protected override BTResult OnExecute(Blackboard bb)
        {
            if (!TargetManager.HasInstance)
            {
                Debug.LogWarning("[BT] BTFindNearestTarget: TargetManager 不存在");
                return BTResult.Failure;
            }

            Transform self = bb.Get<Transform>("_transform");
            if (self == null)
                return BTResult.Failure;

            string key = string.IsNullOrEmpty(Data.TargetKey)
                ? "target"
                : Data.TargetKey;

            float maxRange = Data.MaxRange > 0f ? Data.MaxRange : 15f;

            Targetable found = TargetManager.Instance.FindNearest(
                self.position,
                maxRange,
                Data.TargetTeam,
                bb.Get<GameObject>("_owner")   // 排除自己
            );

            if (found != null)
            {
                bb.Set(key, found.transform);
                Debug.Log($"[BT] BTFindNearestTarget: 找到目标 {found.name} "
                          + $"(阵营:{found.Team}, 已在黑板写入 \"{key}\")");
                return BTResult.Success;
            }

            // 调试：打印注册表中所有目标，帮助定位"为什么找不到"
            int totalCount = TargetManager.Instance.AllTargets.Count;
            Debug.Log($"[BT] BTFindNearestTarget: 未找到目标 "
                      + $"(注册表共{totalCount}个, 搜索阵营:{Data.TargetTeam}, 范围:{maxRange}m, 我的位置:{self.position})");
            return BTResult.Failure;
        }
    }
}
