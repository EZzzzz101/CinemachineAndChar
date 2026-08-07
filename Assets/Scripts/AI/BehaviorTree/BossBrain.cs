using AI.BehaviourTree;
using UnityEngine;

/// <summary>
/// Boss 大脑 — 管理血量、阶段、受击计时
///
/// 职责：
///   - 存 HP / MaxHP
///   - 每帧更新黑板：_hpRatio, _timeSincePlayerAttack
///   - 提供 TakeDamage 接口（给玩家攻击调用，实现 IDamageable 可被命中结算）
/// </summary>
public class BossBrain : MonoBehaviour, IDamageable
{
    int id =100;
    [Header("血量")]
    public float MaxHP = 100f;
    public float CurrentHP { get; private set; }
    /// <summary>是否已死亡（血量<=0 触发一次；死亡后不再受伤/不再播受击）</summary>
    public bool IsDead { get; private set; }

    [Header("阶段阈值（血量比例）")]
    [Tooltip("超过此比例 → Phase 1，低于 → Phase 2，以此类推")]
    public float[] PhaseThresholds = { 0.75f, 0.5f, 0.25f };

    [Header("受击反馈")]
    [Tooltip("受击特效（自身播放）")]
    [SerializeField] private GameObject hitVfxPrefab;

    [Header("攻击配置")]
    [Tooltip("每段攻击的伤害/时机/音效/特效，写入黑板供 BTAttack 节点读取")]
    [SerializeField] private MonsterAttackConfigSO _attackConfig;
    public MonsterAttackConfigSO AttackConfig => _attackConfig;

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
            _bt.Blackboard.Set("_attackConfig", _attackConfig);   // BTAttack 节点读取
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
        if (IsDead) return;   // 死亡后不再结算伤害/受击反馈

        CurrentHP = Mathf.Max(0, CurrentHP - damage);

        EventBus.Emit(
            GameEvents.HPChanged,
            new HPData(id,CurrentHP,MaxHP)
        );
        
        _lastPlayerAttackTime = Time.time;  // 记录玩家攻击时间

        // 死亡：发"胜利结算"事件，行为树血量<=0 分支接管播死亡动画，不再播受击反馈
        if (CurrentHP <= 0f)
        {
            IsDead = true;
            EventBus.Emit(GameEvents.EnemyDied);
            return;
        }

        // 受击特效（自身播放）
        if (hitVfxPrefab != null)
        {
            GameObject vfx = Instantiate(hitVfxPrefab, transform.position + Vector3.up * 1f, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        // 霸体：当前攻击不可被打断，不进受击动画/硬直（仍扣血）
        if (_bt != null && _bt.Blackboard != null && _bt.Blackboard.Get<bool>("_superArmor"))
            return;

        // 触发受击动画
        Animator anim = GetComponent<Animator>();
        if (anim != null)
            anim.SetTrigger("Hit");
    }
}
