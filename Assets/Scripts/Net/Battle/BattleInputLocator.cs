using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 本地玩家输入定位器 — 多人同屏（主机上有本地 + 远端角色）时，
/// FindObjectOfType&lt;PlayerInput&gt;() 会找到任意一个，必须显式区分"本地玩家"。
/// 优先找非远端且 PlayerInput 启用的角色，兜底退回 FindObjectOfType。
/// </summary>
public static class BattleInputLocator
{
    public static PlayerInput FindLocalPlayerInput()
    {
        var controllers = Object.FindObjectsOfType<PlayerController>();
        foreach (var pc in controllers)
        {
            if (pc == null || pc.IsRemote) continue;
            if (pc.PlayerInput != null && pc.PlayerInput.enabled)
                return pc.PlayerInput;
        }
        return Object.FindObjectOfType<PlayerInput>();
    }
}
