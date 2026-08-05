using UnityEngine;

/// <summary>
/// 动画事件接收器 — 挂在与 Animator 同一个 GameObject 上（角色根节点）。
/// Unity 动画事件按方法名自动调用这里的方法（SendMessage 式），这里统一接收再转发到对应系统。
/// 注意：方法名不能与同 GameObject 上其他组件的方法重名，否则会重复触发。
/// </summary>
public class CharacterAnimationEvents : MonoBehaviour
{
    private PlayerController _controller;

    private void Awake()
    {
        // 不缓存 Action：PlayerController.Awake() 的调用顺序不定，运行时再取，此时一定已初始化
        _controller = GetComponent<PlayerController>();
    }

    // —— 连招攻击事件（动画剪辑关键帧调用，无参）——

    /// <summary>核心打击：伤害判定、震屏、顿帧、受击特效（当前连招段）</summary>
    public void ATK() => _controller.Action.ComboState.ATK();

    /// <summary>打开预输入窗口，允许记录玩家按键</summary>
    public void EnablePreInput() => _controller.Action.ComboState.EnablePreInput();

    /// <summary>攻击冷却结束，允许缓冲的按键执行连段</summary>
    public void CancelAttackColdTime() => _controller.Action.ComboState.CancelAttackColdTime();

    /// <summary>禁止连招（收刀段）</summary>
    public void DisableLinkCombo() => _controller.Action.ComboState.DisableLinkCombo();

    /// <summary>允许移动打断</summary>
    public void EnableMoveInterrupt() => _controller.Action.ComboState.EnableMoveInterrupt();
}
