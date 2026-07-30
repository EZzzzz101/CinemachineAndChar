using AI.BehaviourTree;
using UnityEngine;

/// <summary>
/// Boss 大脑 — 管理血量、阶段、受击计时
///
/// 职责：
///   - 存 HP / MaxHP
///   - 每帧更新黑板：_hpRatio, _timeSincePlayerAttack
///   - 提供 TakeDamage 接口（给玩家攻击调用）
/// </summary>
public class BossBrain : MonoBehaviour
{
    [Header("血量")]
    public float MaxHP = 100f;
    public float CurrentHP { get; private set; }

    [Header("阶段阈值（血量比例）")]
    [Tooltip("超过此比例 → Phase 1，低于 → Phase 2，以此类推")]
    public float[] PhaseThresholds = { 0.75f, 0.5f, 0.25f };

    [Header("对峙参数")]
    [Tooltip("玩家长时间不攻击超过此秒数，高概率进入对峙模式")]
    public float ConfrontationDelay = 5f;

    private BehaviorTreeRunner _bt;
    private float _lastPlayerAttackTime;

    /// <summary>当前阶段索引：0=Phase1, 1=Phase2, 2=Phase3, 3=Phase4</summary>
    public int CurrentPhase { get; private set; }

    void Awake()
    {
        _bt = GetComponent<BehaviorTreeRunner>();
        CurrentHP = MaxHP;
        _lastPlayerAttackTime = Time.time;
        CurrentPhase = 0;
    }

    void Start()
    {
        // 等一帧让 BehaviorTreeRunner 初始化完黑板
        if (_bt != null && _bt.Blackboard != null)
        {
            WriteToBlackboard();
        }
    }

    void Update()
    {
        if (_bt == null || _bt.Blackboard == null) return;

        // 更新血量阶段
        float ratio = CurrentHP / MaxHP;
        CurrentPhase = 0;
        for (int i = 0; i < PhaseThresholds.Length; i++)
        {
            if (ratio < PhaseThresholds[i])
                CurrentPhase = i + 1;
        }

        WriteToBlackboard();
    }

    private void WriteToBlackboard()
    {
        var bb = _bt.Blackboard;
        bb.Set("_hpRatio", CurrentHP / MaxHP);
        bb.Set("_timeSincePlayerAttack", Time.time - _lastPlayerAttackTime);
    }

    /// <summary>受伤时调用（由玩家攻击触发）</summary>
    public void TakeDamage(float damage, GameObject attacker)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - damage);
        _lastPlayerAttackTime = Time.time;  // 记录玩家攻击时间

        // 触发受击动画
        Animator anim = GetComponent<Animator>();
        if (anim != null)
            anim.SetTrigger("Hit");
    }
}
