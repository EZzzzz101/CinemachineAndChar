using UnityEngine;

/// <summary>
/// 怪物攻击每段数据 — 可序列化，在 MonsterAttackConfigSO 的列表里编辑。
/// 结构对齐玩家 ComboStepData（动画名/伤害/音效/特效），BTAttack 按索引取用。
/// </summary>
[System.Serializable]
public class MonsterAttackStepData
{
    [Header("动画")]
    [Tooltip("Animator 里的攻击状态名（与 BTAttack.StateNames 对应，如 Attcak1~6）")]
    public string animStateName;

    [Header("霸体")]
    [Tooltip("勾选 = 该攻击不可被打断：受击时不会切受击动画/硬直，攻击继续")]
    public bool isSuperArmor;

    [Header("命中时机")]
    [Tooltip("每次命中的归一化时间点(0~1)，按时间先后排列。如 [0.2, 0.5, 0.8] = 动画 20%/50%/80% 各打一下；空/未填则默认 0.3 打一下")]
    public float[] hitTimes;

    [Header("伤害判定")]
    [Tooltip("每次命中的伤害")]
    public float damage = 10f;

    [Tooltip("命中判定球半径(OverlapSphere)。<=0 用代码兜底 2.5")]
    public float attackRange = 5f;

    [Tooltip("前方锥形角度(全角,度)。<=0 用代码兜底 80")]
    public float attackAngle = 80f;

    [Tooltip("判定起点垂直偏移(相对怪物脚底)")]
    public float attackUpOffset = 1f;

    [Header("音效（可空）")]
    public AudioClip attackSound;               // 起手音效（触发攻击时播）

    [Tooltip("挥空音，在hitTimes触发时播放")]
    public AudioClip swingSound;

    [Tooltip("挥空音音量")]
    [Range(0,1)]
    public float swingVolume = 1f;

    [Tooltip("挥空音空间化")]
    [Range(0,1)]
    public float swingSpatialBlend = 1f;

    public AudioClip[] voiceClips;              // 吼声（随机）
    // 受击反馈（命中音/特效/震屏/顿帧）已解耦到被击者自己播放，这里不再配置
}

/// <summary>
/// 怪物攻击配置 ScriptableObject — 集中管理每段攻击的伤害/时机/音效/特效。
/// 右键 → Create → Combo → MonsterAttackConfig 创建。
/// BossBrain 持有引用并写入黑板，BTAttack 节点读取。
/// </summary>
[CreateAssetMenu(fileName = "MonsterAttackConfig", menuName = "Combo/MonsterAttackConfig")]
public class MonsterAttackConfigSO : ScriptableObject
{
    [Header("基本信息")]
    public string configName;

    [Tooltip("命中层(LayerMask)。不设则查所有层，靠 IDamageable 接口过滤 + 自排除兜底")]
    public LayerMask targetLayer;

    [Header("攻击提示闪光")]
    [Tooltip("怪物起手攻击时在锁定点生成的预警特效预制体（如 PS_SlashCircle_Dark），留空不闪")]
    public GameObject telegraphVfxPrefab;

    [Tooltip("预警音效（所有攻击共用，起手/每段命中前播放）")]
    public AudioClip telegraphSound;

    [Header("攻击列表")]
    public MonsterAttackStepData[] steps;       // 每段数据，索引与 BTAttack 随机挑中的攻击序号对齐
}
