using UnityEngine;

/// <summary>
/// 单次命中结算数据 — 由 AttackHitHelper 计算后经 EventBus(GameEvents.HitLanded) 发出，
/// 伤害跳字等表现层订阅读取。
/// </summary>
public struct DamageData
{
    public float damage;        // 实际结算伤害（暴击时已乘暴伤倍率）
    public bool isCritical;     // 是否暴击
    public Vector3 hitPoint;    // 命中点（世界坐标），供伤害跳字定位

    public DamageData(float damage, bool isCritical, Vector3 hitPoint)
    {
        this.damage = damage;
        this.isCritical = isCritical;
        this.hitPoint = hitPoint;
    }
}
