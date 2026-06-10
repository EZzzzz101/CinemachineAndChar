using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 纯输入读取器 — 单一职责：从 Input System 拿数据
/// </summary>
public class MoveInputMY : MonoBehaviour
{
    private PlayerInput _playerInput;
    private InputAction _moveAction;

    /// <summary>输入值</summary>
    public Vector2 MoveValue { get; private set; }

    /// <summary>是否按住冲刺键</summary>
    public bool IsSprinting => Keyboard.current != null
                            && Keyboard.current.shiftKey.isPressed;

    void Start()
    {
        _playerInput = GetComponent<PlayerInput>();
        _moveAction  = _playerInput.actions.FindAction("Player/Move");
    }

    void Update()
    {
        MoveValue = _moveAction.ReadValue<Vector2>();
    }
}