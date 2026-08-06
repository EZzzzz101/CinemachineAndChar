using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 角色主控 — 持有两个状态机，是 Animator Event 的接收端
/// </summary>
public class PlayerController : MonoBehaviour, IDamageable
{
    [Header("配置")]
    public float SpeedSmoothTime=0.2f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("血量")]
    public float MaxHP = 100f;
    public float CurrentHP { get; private set; }

    [Header("闪避无敌帧")]
    [Tooltip("闪避开始后无敌持续的秒数")]
    [SerializeField] private float _dodgeInvincibleTime = 0.4f;
    public float DodgeInvincibleTime => _dodgeInvincibleTime;
    /// <summary>闪避无敌帧中（受击免疫）。由 DashingState 开/关</summary>
    public bool IsInvincible { get; set; }

    [Header("锁定战斗")]
    [SerializeField] private float _flashMaxDist = 3f;      // 闪身最大距离
    [SerializeField] private float _flashTargetDist = 1.5f; // 闪身后离敌人多远

    [Header("贴地")]
    [SerializeField] private float _groundSnapSpeed = 2f;   // 无跳跃:每帧向下按压力度,把角色贴到地面

    private CharacterController _controller;

    public Animator          Animator      { get; private set; }
    public MoveInputMY         MoveInput     { get; private set; }
    public PlayerInput       PlayerInput   { get; private set; }

    public LocomotionStateMachine Locomotion { get; private set; }
    public ActionStateMachine     Action     { get; private set; }

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

        Locomotion = new LocomotionStateMachine(this);
        Action     = new ActionStateMachine(this,comboConfigSO);
    }

    void Start()
    {
        CurrentHP = MaxHP;
        Locomotion.ChangeState(Locomotion.IdleState);
        Action.ChangeState(Action.ActionNullState);
    }

    void Update()
    {
        Locomotion.Update();
        Action.Update();
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
        // 闪避无敌帧：免疫伤害不进硬直
        if (IsInvincible) return;

        CurrentHP = Mathf.Max(0f, CurrentHP - damage);

        // 受击打断移动/冲刺，避免动画与 FSM 失步
        Locomotion.ChangeState(Locomotion.IdleState);
        // 进受击硬直（若已在受击中则 Hit 重入，动画重播延续硬直）
        Action.ChangeState(Action.HitState);
    }


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

        // 闪到敌人正前方
        Vector3 flashPos = target.GetLockOnPosition() - toTarget.normalized * _flashTargetDist;
        flashPos.y = transform.position.y;
        transform.position = flashPos;
    }

    // ===== 角色转向 =====

    public void HandleRotation()
    {
        Vector2 input = MoveInput.MoveValue;
        if (input.magnitude < 0.1f) return;

        Vector3 moveDir = CameraManager.Instance.GetMoveDir(input);

        transform.rotation = Quaternion.Slerp(
            transform.rotation, Quaternion.LookRotation(moveDir),
            Time.deltaTime * rotationSpeed
        );
    }

}
