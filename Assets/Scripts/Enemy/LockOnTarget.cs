using UnityEngine;

/// <summary>
/// 锁定目标组件 — 提供锁定位移 + 被锁定时的视觉反馈
/// 注意：不再维护全局目标列表（已迁移到 TargetManager + Targetable）
/// </summary>
public class LockOnTarget : MonoBehaviour
{
    [Header("锁定点")]
    [Tooltip("偏移量（相对于敌人位置），通常设在胸口")]
    public Vector3 lockOnPointOffset = new Vector3(0, 1f, 0);

    /// <summary>获取世界空间锁定点坐标</summary>
    public Vector3 GetLockOnPosition()
    {
        return transform.position + lockOnPointOffset;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetLockOnPosition(), 0.15f);
    }
}
