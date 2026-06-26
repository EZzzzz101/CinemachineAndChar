using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 角色主控 — 持有两个状态机，是 Animator Event 的接收端
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("配置")]
    public float SpeedSmoothTime=0.2f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("锁定战斗")]
    [SerializeField] private float _flashMaxDist = 3f;      // 闪身最大距离
    [SerializeField] private float _flashTargetDist = 1.5f; // 闪身后离敌人多远

    public Animator          Animator      { get; private set; }
    public MoveInputMY         MoveInput     { get; private set; }
    public PlayerInput       PlayerInput   { get; private set; }

    public LocomotionStateMachine Locomotion { get; private set; }
    public ActionStateMachine     Action     { get; private set; }

    public ComboConfigSO comboConfigSO;


    //动画枚举动作
    public AnimationEnterBehaviour.AnimationEnterState LastAnimEnterState { get; private set; }


    void Awake()
    {
        Animator    = GetComponent<Animator>();
        MoveInput   = GetComponent<MoveInputMY>();
        PlayerInput = GetComponent<PlayerInput>();

        Locomotion = new LocomotionStateMachine(this);
        Action     = new ActionStateMachine(this,comboConfigSO);
    }

    void Start()
    {
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
        transform.position += Animator.deltaPosition;
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

        }
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

     #region 连招动画事件
     /// <summary>
     /// 打开预输入窗口
     /// </summary>
        public void EnablePreInput()
        {
            Action.ComboState.EnablePreInput();
        }
        /// <summary>
        /// 攻击后摇结束，可以闪避/做其他动作
        /// </summary>
        public void CancelAttackColdTime()
        { 
            Action.ComboState.CancelAttackColdTime();
        }
        /// <summary>
        /// 禁止连招
        /// </summary>
        public void DisableLinkCombo()
        { 
            Action.ComboState.DisableLinkCombo();
        }
        /// <summary>
        /// 允许移动打断
        /// </summary>
        public void EnableMoveInterrupt()
        {
             Action.ComboState.EnableMoveInterrupt();
        }
     
        public void ATK()
        {
             Action.ComboState.ATK();
        }
    #endregion
}
