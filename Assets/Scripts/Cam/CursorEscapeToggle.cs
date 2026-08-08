using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ESC 呼出/收回鼠标（游戏内没有 UI 打开时）：
/// 按 ESC 显示鼠标并冻结角色操控，再按 ESC 或点击游戏画面恢复锁定。
/// 有弹窗/组队界面打开时，ESC 交给界面自己处理，不抢。
/// 解决打包后游戏内 ESC 呼不出鼠标的问题。
/// </summary>
public class CursorEscapeToggle : GameModule<CursorEscapeToggle>
{
    private bool _cursorFreed;

    protected override void OnInit()
    {
        Debug.Log("[CursorEscapeToggle] 初始化完成");
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (AnyEscHandlingViewOpen()) return;   // 界面自己处理 ESC（关弹窗/退组队）
            FreeCursor(!_cursorFreed);
        }

        // 自由鼠标状态下，点击游戏画面 → 收回鼠标恢复操控
        if (_cursorFreed && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
            && !AnyEscHandlingViewOpen())
        {
            FreeCursor(false);
        }
    }

    private void FreeCursor(bool freed)
    {
        _cursorFreed = freed;
        if (freed) PlayerInputGate.EnterUI();
        else PlayerInputGate.ExitUI();
    }

    private bool AnyEscHandlingViewOpen()
    {
        return IsOpen<TeamUpView>() || IsOpen<AddView>() || IsOpen<BeInvitedView>();
    }

    private static bool IsOpen<T>() where T : UIView
    {
        var view = UIManager.Instance.Get<T>();
        return view != null && view.gameObject.activeSelf;
    }
}
