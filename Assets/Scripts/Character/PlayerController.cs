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
            case AnimationExitBehaviour.AnimExitState.Atk:
                Action.OnAnimationExitEvent();
                break;
        }
    }


    //角色转向
    public void HandleRotation()
    {
        Vector2 input = MoveInput.MoveValue;
        if (input.magnitude < 0.1f) return;

        Transform cam = Camera.main.transform;
        Vector3 forward = cam.forward; forward.y = 0;
        Vector3 right   = cam.right;   right.y = 0;
        Vector3 moveDir = (forward * input.y + right * input.x).normalized;

        transform.rotation = Quaternion.Slerp(
            transform.rotation, Quaternion.LookRotation(moveDir),
            Time.deltaTime * rotationSpeed
        );
    }
}
