/// <summary>
/// 动作状态翻译器（纯 C#）— 主机侧把角色的 FSM 状态压缩成快照里的 BattleAnimState 枚举。
///
/// 为什么需要它：
/// 1) 快照只传 1 字节枚举，不传 Animator 状态名（长字符串、两端命名强依赖）；
/// 2) 优先级规则（死亡 > 受击 > 攻击 > 闪避 > ...）是协议语义，必须可独立测试，
///    不能埋进 MonoBehaviour 里随场景走；
/// 3) 以后加新动作（倒地、硬直变体等）只改这一处 + 枚举，快照字段结构不动。
/// </summary>
public static class BattleAnimMapper
{
    /// <summary>
    /// 由一组布尔标志推出压缩枚举。调用方（BattleHostRuntime）从角色的状态机取标志。
    /// 优先级从上到下：死亡最高，待机最低——"高优先级状态优先占位"。
    /// </summary>
    public static BattleAnimState FromFlags(
        bool isDead, bool isHit, bool isAttacking,
        bool isDashing, bool isSprinting, bool isRunning, bool isTurnBack)
    {
        if (isDead) return BattleAnimState.Dead;        // 死亡不可打断，优先级最高
        if (isHit) return BattleAnimState.Hit;          // 受击硬直锁操作
        if (isAttacking) return BattleAnimState.Attack; // 连招进行中
        if (isDashing) return BattleAnimState.Dash;     // 闪避（无敌帧期间）
        if (isSprinting) return BattleAnimState.Sprint;
        if (isTurnBack) return BattleAnimState.TurnBack;
        if (isRunning) return BattleAnimState.Run;
        return BattleAnimState.Idle;
    }

    /// <summary>
    /// 幽灵端翻译：枚举 → Animator CrossFade 目标状态名。
    /// Idle/Run/Sprint 不是一次性动画，不走 CrossFade（用 SetBool+Movement 驱动），返回 null。
    /// Attack 的动画名来自 Combo 配置（各角色不同），由调用方传入。
    /// </summary>
    public static string CrossFadeName(BattleAnimState state, string attackAnimName = null)
    {
        switch (state)
        {
            case BattleAnimState.Dash:     return "DashFront";
            case BattleAnimState.TurnBack: return "TurnBack";
            case BattleAnimState.Attack:   return attackAnimName ?? "Attack";
            case BattleAnimState.Hit:      return "BeHit";
            case BattleAnimState.Dead:     return "Death";
            default:                       return null;   // Idle / Run / Sprint
        }
    }
}
