using UnityEngine;

/// <summary>
/// 客户端 Boss 幽灵的"受击表现接口"。
///
/// 为什么需要它：BossBrain（含 IDamageable）在客户端被销毁（主机权威，防双端分叉），
/// 玩家攻击时 AttackHitHelper 找不到目标 → 挥空。本组件提供轻量 IDamageable：
/// 受击只播动画/特效（表现），不真扣血——Boss HP 由主机快照/事件权威下发。
/// </summary>
public class BossGhostBrain : MonoBehaviour, IDamageable
{
    private Animator _animator;

    [Tooltip("受击特效 prefab（可选，走 VFXPool 对象池）")]
    public GameObject hitVfxPrefab;   // 动态 AddComponent 时由 BattleClientRuntime 从原 BossBrain 拷贝

    /// <summary>客机命中回调：BattleClientRuntime 注入，命中时把"我打到了"上报主机（M11 伤害闭环）</summary>
    public System.Action<GameObject, float> OnBossHit;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        if (hitVfxPrefab != null)
            VFXPool.Prewarm(hitVfxPrefab, 2);
    }

    /// <summary>
    /// 玩家命中 Boss 幽灵：只做表现（受击动画/特效），不扣血。
    /// 真实伤害由主机判定后广播（M11 第 2 步），HP 以主机快照为准。
    /// 命中同时通过 OnBossHit 上报主机，主机宽容判定后扣真 Boss 血。
    /// </summary>
    public void TakeDamage(float damage, GameObject attacker)
    {
        // 受击动画不在这里播：由 BattleGhostInterpolator 按主机快照 AnimHash 驱动，
        // 避免两个组件抢 Animator（否则动画会乱跳）。这里只做本地打击特效（表现层）。
        if (hitVfxPrefab != null)
            VFXPool.Spawn(hitVfxPrefab, transform.position + Vector3.up * 1.2f, Quaternion.identity, null, 2f);

        // 命中上报：让主机判定扣血（客机本地打中 ≠ 主机判定，必须显式上报）
        OnBossHit?.Invoke(attacker, damage);

        Debug.Log("[BossGhost] 玩家命中 Boss 幽灵（已上报主机判定）");
    }
}
