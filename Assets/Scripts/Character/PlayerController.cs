using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 角色主控 — 持有两个状态机，是 Animator Event 的接收端
/// </summary>
public class PlayerController : MonoBehaviour, IDamageable
{
    /// <summary>
    /// 血条/结算用 id（GamePanel 用 id==1 显示玩家血条、id==100 显示 Boss 血条）。
    /// 联机时 Remote 克隆必须分配不同 id（2、3...），否则它的 HP 事件会串到主机自己的血条上。
    /// </summary>
    public int id = 1;
    [Header("配置")]
    public float SpeedSmoothTime=0.2f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("基础属性")]
    [Tooltip("攻击力/生命力/暴击率/暴击伤害，统一在此配置")]
    public CharacterBaseAttribute baseAttribute = new CharacterBaseAttribute();

    /// <summary>最大生命值（从基础属性读取）</summary>
    public float MaxHP => baseAttribute != null ? baseAttribute.maxHP : 100f;
    public float CurrentHP { get; private set; }
    /// <summary>是否已发过"失败结算"事件（血量首次归零时发一次，角色行为不受影响）</summary>
    private bool _playerDiedEventSent;

    [Header("受击反馈")]
    [Tooltip("受击特效（角色自己播放；闪避成功不播）")]
    [SerializeField] private GameObject hitVfxPrefab;
    [Tooltip("受击命中音（角色自己播放；闪避成功不播）")]
    [SerializeField] private AudioClip hitSound;
    [Tooltip("受击命中音音量")]
    [Range(0, 1)]
    [SerializeField] private float hitVolume = 1f;
    [Tooltip("受击命中音空间度(0=2D立体声,1=全3D)")]
    [Range(0, 1)]
    [SerializeField] private float hitSpatialBlend = 0f;
    [Tooltip("受击震屏力度")]
    [SerializeField] private float hitShake = 0.5f;
    [Tooltip("受击顿帧时长(秒)")]
    [SerializeField] private float hitPauseDuration = 0.05f;

    [Header("闪避无敌帧")]
    [Tooltip("闪避开始后无敌持续的秒数")]
    [SerializeField] private float _dodgeInvincibleTime = 0.4f;
    public float DodgeInvincibleTime => _dodgeInvincibleTime;
    /// <summary>闪避无敌帧中（受击免疫）。由 DashingState 开/关</summary>
    public bool IsInvincible { get; set; }

    [Header("完美闪避·时停")]
    [Tooltip("完美闪避触发时停的倍率(0~1，越小越慢)，可调")]
    [SerializeField] private float _perfectDodgeTimeScale = 0.2f;
    [Tooltip("完美闪避时停持续的秒数(真实秒)")]
    [SerializeField] private float _perfectDodgeDuration = 0.5f;
    [Tooltip("完美闪避时运动模糊强度(0~1)，随开始时停开启")]
    [SerializeField] private float _perfectDodgeBlurIntensity = 0.5f;
    [Tooltip("完美闪避模糊淡出秒数")]
    [SerializeField] private float _perfectDodgeBlurFade = 0.3f;
    /// <summary>本次闪避是否已触发过完美闪避（每次闪避只时停一次）</summary>
    private bool _perfectDodgeTriggered;

    [Header("锁定战斗")]
    [SerializeField] private float _flashMaxDist = 3f;      // 闪身最大距离
    [SerializeField] private float _flashTargetDist = 1.5f; // 闪身后离敌人多远

    [Header("贴地")]
    [SerializeField] private float _groundSnapSpeed = 2f;   // 无跳跃:每帧向下按压力度,把角色贴到地面

    private CharacterController _controller;

    public Animator          Animator      { get; private set; }
    public MoveInputMY         MoveInput     { get; private set; }
    public PlayerInput       PlayerInput   { get; private set; }

    /// <summary>输入源抽象（M9）：本地玩家=LocalInputProvider，主机上的远端玩家=RemoteInputProvider</summary>
    public IInputProvider Input { get; private set; }
    /// <summary>是否远端角色（主机模拟的别人；true 时禁用本地输入组件）</summary>
    public bool IsRemote { get; private set; }

    /// <summary>统一移动输入读取口：状态机不要再直接碰 Input System</summary>
    public Vector2 MoveValue => Input != null ? new Vector2(Input.MoveX, Input.MoveZ) : Vector2.zero;

    public LocomotionStateMachine Locomotion { get; private set; }
    public ActionStateMachine     Action     { get; private set; }

    /// <summary>受击硬直锁定中（受击动画前 70%：禁止移动/冲刺，强制看受击动画）</summary>
    public bool IsInHitStun => Action != null && Action.CurrentState is HitState hit && hit.IsLocked;
    /// <summary>攻击挥击锁定转向（段切换给短暂脉冲放开，供瞄准下一段；其余挥击时间锁定）</summary>
    public bool IsTurnLocked => Action != null && Action.CurrentState is ATKingState atk && atk.IsTurnLocked;

    public PlayerAudio     PlayerAudio     { get; private set; }
    public ComboConfigSO comboConfigSO;


    //动画枚举动作
    public AnimationEnterBehaviour.AnimationEnterState LastAnimEnterState { get; private set; }

    void Awake()
    {
        Animator    = GetComponent<Animator>();
        MoveInput   = GetComponent<MoveInputMY>();
        PlayerInput = GetComponent<PlayerInput>();
        _controller = GetComponent<CharacterController>();
        PlayerAudio = GetComponent<PlayerAudio>();

        // 默认本地输入：每个玩家 prefab 实例都有独立的 PlayerInput，各读各的
        Input = PlayerInput != null ? new LocalInputProvider(PlayerInput) : null;

        if (hitVfxPrefab != null)
            VFXPool.Prewarm(hitVfxPrefab, 2);

        Locomotion = new LocomotionStateMachine(this);
        Action     = new ActionStateMachine(this,comboConfigSO);
        CurrentHP = MaxHP;
    }

    void Start()
    {
        
        Locomotion.ChangeState(Locomotion.IdleState);
        Action.ChangeState(Action.ActionNullState);
    }

    void Update()
    {
        Input?.Tick();        // 采集本帧输入（远端 provider 为空实现，状态由网络 Apply 推入）
        Locomotion.Update();
        Action.Update();
        Input?.EndFrame();    // 消费边沿：防下一帧重复触发
    }

    /// <summary>
    /// 主机把"别人的角色"绑定为远端输入：替换 provider + 禁用本地输入组件，
    /// 这样同一份 PlayerController 就被遥控驱动了（M9 的核心）。
    /// </summary>
    public void BindRemoteInput(RemoteInputProvider provider)
    {
        Input = provider;
        IsRemote = true;
        if (MoveInput != null) MoveInput.enabled = false;
        if (PlayerInput != null) PlayerInput.enabled = false;
    }

    void OnAnimatorMove()
    {
        // 每帧位移照旧累计,只是改经 CharacterController 施加:撞到墙会被挡住
        Vector3 delta = Animator.deltaPosition;
        if (_controller != null)
        {
            // 无跳跃:每帧轻微向下按压,让角色始终贴地(被地面碰撞体挡住即站立)
            _controller.Move(delta + Vector3.down * _groundSnapSpeed * Time.deltaTime);
        }
        else
        {
            // 兜底:还没用「一键地面层+碰撞」跑过接线时,保持原来的纯位移行为
            transform.position += delta;
        }
    }

    // 动画进入 → FSM 路由
    public void OnAnimationTranslateEvent(AnimationEnterBehaviour.AnimationEnterState targetState)
    {
        LastAnimEnterState = targetState;
        switch (targetState)
        {
            case AnimationEnterBehaviour.AnimationEnterState.DashFront:
                Locomotion.OnAnimationTranslateEvent(Locomotion.DashingState);
                break;
            case  AnimationEnterBehaviour.AnimationEnterState.DashBack:
                Locomotion.OnAnimationTranslateEvent(Locomotion.DashingState);
                break;
            case AnimationEnterBehaviour.AnimationEnterState.TurnBack:
                Locomotion.OnAnimationTranslateEvent(Locomotion.TurnBackState);
                break;
            case  AnimationEnterBehaviour.AnimationEnterState.Atk:
                Action.OnAnimationTranslateEvent(Action.ComboState);
                break;
            // 以后新增动画驱动状态在这加 case
        }
    }

    // 动画退出 → FSM 路由（只通知对应状态机）
    public void OnAnimationExitEvent(AnimationExitBehaviour.AnimExitState exitState)
    {
        switch (exitState)
        {
            case AnimationExitBehaviour.AnimExitState.Dash:
                Locomotion.OnAnimationExitEvent();
                break;
            case AnimationExitBehaviour.AnimExitState.TurnBack:
                Locomotion.OnAnimationExitEvent();
                break;
            case AnimationExitBehaviour.AnimExitState.Atk:
                Action.OnAnimationExitEvent();
                break;
            case AnimationExitBehaviour.AnimExitState.Hit:
                Action.OnAnimationExitEvent();
                break;

        }
    }


    // ===== 受击 =====

    /// <summary>受击（由怪物攻击触发）：扣血 + 打断移动 + 进受击硬直</summary>
    public void TakeDamage(float damage, GameObject attacker)
    {
        // 闪避无敌帧：免疫伤害不进硬直；若恰好盖住攻击命中瞬间 → 完美闪避 + 时停
        if (IsInvincible)
        {
            if (!_perfectDodgeTriggered)
            {
                _perfectDodgeTriggered = true;
                Debug.Log($"[PerfectDodge] 完美闪避成功！无敌帧盖住命中 → 时停(timeScale={_perfectDodgeTimeScale:F2}, {_perfectDodgeDuration:F2}s) | 攻击者={(attacker != null ? attacker.name : "null")}");
                HitPauseManager.Instance.Trigger(_perfectDodgeDuration, _perfectDodgeTimeScale);
                CameraShake.Instance.TriggerShake(0.3f);
                // 完美闪避模糊：冲顶→回落→时停保持→曲线淡出（内部按真实时间推进）
                DashMotionBlur.Instance.Play(_perfectDodgeBlurIntensity, _perfectDodgeDuration, _perfectDodgeBlurFade);
            }
            return;
        }

        CurrentHP = Mathf.Max(0f, CurrentHP - damage);
        ApplyHpEffects();
    }

    /// <summary>
    /// 网络伤害应用（主机权威）— 客户端收到 BattleEvent(Damage) 时调用。
    /// 为什么不用 TakeDamage：主机已经判定过无敌帧/命中，客户端若再走本地无敌帧判定会不一致；
    /// 而且伤害值要以主机广播的新 HP 为准（v2），而不是本地再算一遍（v1 仅用于日志/表现）。
    /// </summary>
    public void ApplyNetworkDamage(float damage, float newHp)
    {
        CurrentHP = Mathf.Max(0f, newHp);
        ApplyHpEffects();
    }

    /// <summary>扣血后的公共处理：发 HP 事件 → 死亡结算 → 受击反馈（本地/网络伤害共用）</summary>
    private void ApplyHpEffects()
    {
        //通知ui改变血条
        EventBus.Emit(
            GameEvents.HPChanged,
            new HPData(id,CurrentHP,MaxHP)
        );

        //通知ui改变血量数字
        EventBus.Emit(
            GameEvents.HPTextChanged,
            new HPData(id,CurrentHP,MaxHP)
        );

        // 暂时不处理角色死亡：血量见底不影响任何行为，只发一次"失败结算"事件供 UI 使用
        if (CurrentHP <= 0f && !_playerDiedEventSent)
        {
            _playerDiedEventSent = true;
            EventBus.Emit(GameEvents.PlayerDied);
        }

        PlayHitFeedback();
    }

    /// <summary>受击反馈（角色自己播；闪避成功/无敌帧不会走到这里）</summary>
    private void PlayHitFeedback()
    {
        // 受击反馈（角色自己播放；闪避成功/无敌帧不会走到这里）
        if (hitVfxPrefab != null)
        {
            VFXPool.Spawn(hitVfxPrefab, transform.position + Vector3.up * 1f, Quaternion.identity, null, 2f);
        }
        if (hitSound != null && PlayerAudio != null)
            PlayerAudio.PlayHitSound(hitSound, hitVolume, hitSpatialBlend);
        CameraShake.Instance.TriggerShake(hitShake);
        HitPauseManager.Instance.Trigger(hitPauseDuration);

        // 受击打断移动/冲刺，避免动画与 FSM 失步
        Locomotion.ChangeState(Locomotion.IdleState);
        // 进受击硬直（若已在受击中则 Hit 重入，动画重播延续硬直）
        Action.ChangeState(Action.HitState);
    }

    /// <summary>重置完美闪避标记（每次闪避开始时调用，允许再次触发时停）</summary>
    public void ResetPerfectDodge() => _perfectDodgeTriggered = false;


    // ===== 锁定战斗: 闪身 + 面向 =====

    /// <summary>锁定态：瞬间面向锁定敌人</summary>
    public void FaceEnemy()
    {
        if (!LockOnManager.HasInstance || !LockOnManager.Instance.IsLockedOn) return;

        Vector3 dir = LockOnManager.Instance.CurrentTarget.GetLockOnPosition() - transform.position;
        dir.y = 0;
        if (dir.magnitude < 0.01f) return;

        transform.rotation = Quaternion.LookRotation(dir);
    }

    /// <summary>锁定态：够近就闪到敌人正前方，太远只面向不闪</summary>
    public void FlashToEnemy()
    {
        if (!LockOnManager.HasInstance || !LockOnManager.Instance.IsLockedOn) return;

        var target = LockOnManager.Instance.CurrentTarget;
        Vector3 toTarget = target.GetLockOnPosition() - transform.position;
        toTarget.y = 0;
        float dist = toTarget.magnitude;

        if (dist > _flashMaxDist) return; // 太远不闪，原地打

        // 闪到敌人正前方（带碰撞：撞到敌人停在表面，不穿进身体）
        Vector3 flashPos = target.GetLockOnPosition() - toTarget.normalized * _flashTargetDist;
        flashPos.y = transform.position.y;
        if (_controller != null)
            _controller.Move(flashPos - transform.position);
        else
            transform.position = flashPos;
    }

    // ===== 角色转向 =====

    public void HandleRotation()
    {
        Vector2 input = MoveValue;
        if (input.magnitude < 0.1f) return;

        // 本地玩家：摇杆是"相对相机"的输入 → 按自己相机转成世界方向；
        // 远端角色：客户端上报前已把摇杆转成世界方向（x,z），直接使用——
        // 否则主机会用"主机相机"解释客户端摇杆，方向必然错（联机坐标 bug）。
        Vector3 moveDir = IsRemote
            ? new Vector3(input.x, 0f, input.y)
            : CameraManager.Instance.GetMoveDir(input);

        transform.rotation = Quaternion.Slerp(
            transform.rotation, Quaternion.LookRotation(moveDir),
            Time.deltaTime * rotationSpeed
        );
    }

}
