using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 角色输入 + 鼠标开关闸门 — UI（组队、弹窗等）打开时 EnterUI()，关闭时 ExitUI()
/// EnterUI：显示鼠标并把控制权交给 Canvas（Cursor 解锁），禁用 Player 动作表（移动/镜头/攻击全冻结）
/// ExitUI：恢复 Player 动作表，隐藏并锁定鼠标回游戏视角
/// 注意：只禁 Player 图，UI 事件模块用的是独立 actions，不受影响。
/// </summary>
public static class PlayerInputGate
{
    /// <summary>进入 UI 态：显示鼠标 + 冻结角色操控</summary>
    public static void EnterUI()
    {
        SetCursorVisible(true);
        SetPlayerMapEnabled(false);
    }

    /// <summary>退出 UI 态：恢复角色操控 + 隐藏鼠标</summary>
    public static void ExitUI()
    {
        SetPlayerMapEnabled(true);
        SetCursorVisible(false);
    }

    private static void SetPlayerMapEnabled(bool enabled)
    {
        var playerInput = BattleInputLocator.FindLocalPlayerInput();
        var playerMap = playerInput?.actions.FindActionMap("Player");
        if (playerMap == null) return;

        if (enabled) playerMap.Enable();
        else playerMap.Disable();
    }

    private static void SetCursorVisible(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }
}
