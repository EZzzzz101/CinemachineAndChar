using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 本地输入实现 — 从 Input System 读取键盘/手柄。
/// 边沿用 WasPressedThisFrame/WasReleasedThisFrame 轮询（与订阅事件等价，
/// 但可以被网络层"旁听"：Tick 时把按下事件同步回调给 BattleClientRuntime 即时上报）。
/// </summary>
public class LocalInputProvider : IInputProvider
{
    private readonly PlayerInput _playerInput;
    private readonly InputAction _move;
    private readonly InputAction _dash;
    private readonly InputAction _fire;

    public float MoveX { get; private set; }
    public float MoveZ { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool MoveStarted { get; private set; }
    public bool MoveCanceled { get; private set; }
    public bool DashPressed { get; private set; }
    public bool AttackPressed { get; private set; }

    public event Action<BattleInputFlags> OnEdge;

    /// <summary>
    /// 入场缓冲开关：true 时忽略攻击/闪避边沿（链路未稳定前不让角色执行瞬时行为）。
    /// 由 BattleClientRuntime 在解锁后短暂开启。
    /// </summary>
    public bool GateEdges { get; set; }

    public LocalInputProvider(PlayerInput playerInput)
    {
        _playerInput = playerInput;
        if (_playerInput == null) return;
        _move = _playerInput.actions.FindAction("Player/Move");
        _dash = _playerInput.actions.FindAction("Player/Dash");
        _fire = _playerInput.actions.FindAction("Player/Fire");
    }

    public void Tick()
    {
        if (_playerInput == null) return;

        // 连续状态：直接读值
        Vector2 move = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
        MoveX = move.x;
        MoveZ = move.y;
        SprintHeld = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

        // 边沿：本帧是否"刚按下/刚松开"（动作被禁用时恒 false，UI 态自然冻结）
        MoveStarted = _move != null && _move.WasPressedThisFrame();
        MoveCanceled = _move != null && _move.WasReleasedThisFrame();
        DashPressed = _dash != null && _dash.WasPressedThisFrame();
        AttackPressed = _fire != null && _fire.WasPressedThisFrame();

        // 缓冲期：攻击/闪避边沿强制无效（本地不执行，也不会触发 OnEdge 上报）
        if (GateEdges)
        {
            DashPressed = false;
            AttackPressed = false;
        }

        // 边沿事件：网络层订阅它做"即时上报"（攻击/闪避不等到定频才发）
        BattleInputFlags edge = BattleInputFlags.None;
        if (DashPressed) edge |= BattleInputFlags.Dash;
        if (AttackPressed) edge |= BattleInputFlags.Attack;
        if (edge != BattleInputFlags.None)
            OnEdge?.Invoke(edge);
    }

    public void EndFrame()
    {
        MoveStarted = false;
        MoveCanceled = false;
        DashPressed = false;
        AttackPressed = false;
    }
}
