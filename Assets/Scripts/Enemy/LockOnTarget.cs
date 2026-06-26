using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂载在敌人 GameObject 上的锁定目标标记
/// OnEnable/OnDisable 自动注册到静态列表，避免 FindObjectsOfType
/// </summary>
public class LockOnTarget : MonoBehaviour
{
    [Header("锁定点")]
    [Tooltip("偏移量（相对于敌人位置），通常设在胸口")]
    public Vector3 lockOnPointOffset = new Vector3(0, 1f, 0);

    /// <summary>所有活跃的锁定目标（自动维护，O(1) 访问）</summary>
    public static readonly List<LockOnTarget> ActiveTargets = new List<LockOnTarget>();

    void OnEnable()  => ActiveTargets.Add(this);
    void OnDisable() => ActiveTargets.Remove(this);

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
