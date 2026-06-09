using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 角色控制：移动（轮询）+ 跳跃（事件）
/// </summary>
public class InputTest : MonoBehaviour
{
    [Header("移动")]
    [SerializeField] private float moveSpeed = 5f;
    [Header("跳跃")]
    [SerializeField] private float jumpForce = 8f;
    [Header("地面检测")]
    [SerializeField] private float groundCheck = 1f;
    [SerializeField] private LayerMask groundLayer;

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;
    private Rigidbody rb;
    private Vector2 moveInput;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        rb = GetComponent<Rigidbody>();

        moveAction = playerInput.actions.FindAction("Player/Move");
        jumpAction = playerInput.actions.FindAction("Player/Jump");
        jumpAction.performed += OnJump;
    }

    // Update只存输入，不移动
    private void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();
    }

    // 物理移动必须放FixedUpdate
    private void FixedUpdate()
    {
        Vector3 moveDir = new Vector3(moveInput.x, 0, moveInput.y) * moveSpeed;
        moveDir += Vector3.up * rb.velocity.y; // 保留Y轴重力下落速度
        rb.velocity = moveDir;
    }

    // 跳跃：落地才能跳
    private void OnJump(InputAction.CallbackContext ctx)
    {
        if (IsGrounded())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    // 脚底射线检测是否在地面
    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, groundCheck, groundLayer);
    }

    private void OnDestroy()
    {
        jumpAction.performed -= OnJump;
    }
}