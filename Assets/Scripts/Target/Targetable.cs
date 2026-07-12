using UnityEngine;

/// <summary>
/// 可被探测/锁定的生物标记 — 挂载在任何"生物"GameObject 上
/// OnEnable 自动注册到 TargetManager，OnDisable 自动注销
/// </summary>
public enum Team
{
    Player,   // 玩家阵营
    Enemy,    // 敌方阵营
    Neutral   // 中立
}

public class Targetable : MonoBehaviour
{
    [field: SerializeField]
    public Team Team { get; private set; } = Team.Enemy;

    void OnEnable()
    {
        if (TargetManager.HasInstance)
            TargetManager.Instance.Register(this);
    }

    void OnDisable()
    {
        if (TargetManager.HasInstance)
            TargetManager.Instance.Unregister(this);
    }
}
