using UnityEngine;

/// <summary>
/// 可受伤接口 — 命中代码只调它，不依赖具体怪物类。
/// 实现类（如 BossBrain）各自管理血量/减抗/防御等计算。
/// </summary>
public interface IDamageable
{
    void TakeDamage(float damage, GameObject attacker);
}
