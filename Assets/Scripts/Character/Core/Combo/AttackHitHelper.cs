using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 命中检测共享工具 — 玩家/怪物攻击共用的 OverlapSphere 前方锥形命中结算。
/// 抽自 ATKingState.ATK() 的命中逻辑，让怪物攻击"参考角色方法"落到同一份代码。
/// </summary>
public static class AttackHitHelper
{
    /// <summary>
    /// 范围 + 前方锥形 OverlapSphere → 去重收集 IDamageable。
    /// layerMask 为 0 时查所有层（靠 IDamageable 过滤 + attackerRoot 自排除兜底）。
    /// </summary>
    public static HashSet<IDamageable> DetectHits(
        Vector3 origin, Vector3 forward,
        float range, float angle, int layerMask,
        Transform attackerRoot = null)
    {
        var result = new HashSet<IDamageable>();   // 按引用去重：多碰撞体只结算一次
        float r = range > 0f ? range : 2.5f;
        float halfAngle = (angle > 0f ? angle : 80f) * 0.5f;
        int mask = layerMask != 0 ? layerMask : Physics.AllLayers;

        Collider[] cols = Physics.OverlapSphere(origin, r, mask);
        foreach (var col in cols)
        {
            if (col == null) continue;

            // 排除攻击者自身及其子物体（防自伤，如怪物打到自己的 BossBrain）
            if (attackerRoot != null &&
                (col.transform.IsChildOf(attackerRoot) || attackerRoot.IsChildOf(col.transform)))
                continue;

            Vector3 toTarget = col.transform.position - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > r * r) continue;              // 距离
            if (Vector3.Angle(forward, toTarget.normalized) > halfAngle) continue;  // 前方锥形

            var damageable = col.GetComponentInParent<IDamageable>();
            if (damageable == null) continue;

            result.Add(damageable);
        }
        return result;
    }

    /// <summary>
    /// 命中结算：DetectHits + TakeDamage + 受击特效。
    /// 返回是否至少命中一个目标（调用方可据此播命中音/震屏/顿帧）。
    /// </summary>
    public static bool DealDamage(
        Vector3 origin, Vector3 forward,
        float range, float angle, int layerMask,
        float damage, GameObject attacker,
        GameObject hitVfxPrefab = null,
        Transform attackerRoot = null)
    {
        var targets = DetectHits(origin, forward, range, angle, layerMask, attackerRoot);
        bool anyHit = false;

        foreach (var damageable in targets)
        {
            if (damageable == null) continue;
            anyHit = true;
            damageable.TakeDamage(damage, attacker);

            // 受击特效（配置了才播，2s 自毁）
            if (hitVfxPrefab != null)
            {
                var comp = damageable as Component;
                Vector3 hitPos = comp != null
                    ? comp.transform.position + Vector3.up * 1f
                    : attacker.transform.position + attacker.transform.forward * 1f;
                GameObject vfx = Object.Instantiate(hitVfxPrefab, hitPos, Quaternion.identity);
                Object.Destroy(vfx, 2f);
            }

            // 供伤害数字/统计订阅（无订阅者时安全 no-op）
            EventBus.Emit(GameEvents.HitLanded, damage);
        }

        return anyHit;
    }
}
