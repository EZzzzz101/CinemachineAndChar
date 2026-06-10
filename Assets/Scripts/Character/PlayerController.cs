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
    // public ActionStateMachine     Action     { get; private set; }

    void Awake()
    {
        Animator    = GetComponent<Animator>();
        MoveInput   = GetComponent<MoveInputMY>();
        PlayerInput = GetComponent<PlayerInput>();

        Locomotion = new LocomotionStateMachine(this);
        // Action     = new ActionStateMachine(this);
    }

    void Start()
    {
        Locomotion.ChangeState(Locomotion.IdleState);
        // Action.ChangeState(Action.NullState);
    }

    void Update()
    {
        Locomotion.Update();
        // Action.Update();
    }

    void OnAnimatorMove()
    {
        transform.position += Animator.deltaPosition;
    }

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