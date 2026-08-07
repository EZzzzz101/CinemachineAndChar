using UnityEngine;

/// <summary>
/// 角色基础属性 — 从 PlayerController 中拆出，便于在 Inspector 统一配置、代码统一读取。
/// 怪物复用本类时把 critRate 配成 0 即天然不暴击，无需单独写逻辑。
/// </summary>
[System.Serializable]
public class CharacterBaseAttribute
{
    [Header("攻击力")]
    [Tooltip("角色基础攻击力（预留：当前单次命中伤害仍由连招段配置 ComboStepData.damage 决定）")]
    public float attack = 10f;

    [Header("生命力")]
    [Tooltip("最大生命值")]
    public float maxHP = 100f;

    [Header("暴击率")]
    [Tooltip("0~1，0 = 不暴击（怪物默认 0）")]
    [Range(0f, 1f)]
    public float critRate = 0f;

    [Header("暴击伤害")]
    [Tooltip("暴击伤害倍率：1.5 = 造成 150% 伤害")]
    public float critDamage = 1.5f;
}
