using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 幽灵插值器 — 客户端显示"别人的角色"用的纯视觉组件。
///
/// 为什么需要它：别人的角色不在这台机器上模拟（没有 PlayerController/FSM/输入），
/// 它只是一具"皮影"：按快照里的状态枚举播动画、按快照位置做平滑插值。
///
/// 设计要点：
///   1) 动画：Anim 枚举翻译成 SetBool/CrossFade（BattleAnimMapper），动画第几帧由本机 Animator 自己推；
///   2) 位置：不瞬移，向目标 MoveTowards 平滑追（演示版插值；正式可升级为快照缓冲 + 延迟插值）；
///   3) 幽灵化：销毁一切模拟/输入/反馈组件，只留 Animator + 渲染。
/// </summary>
public class BattleGhostInterpolator : MonoBehaviour
{
    /// <summary>缓冲里的一帧快照（位置 + 时间戳）</summary>
    private class SnapPoint
    {
        public float Time;
        public Vector3 Pos;
        public Quaternion Rot;
    }

    private Animator _animator;
    private string _attackAnimName = "Attack";   // 攻击动画名来自 Combo 配置，Setup 时从 prefab 读
    private BattleAnimState _lastAnim = (BattleAnimState)0xFF;   // 初始"无状态"，保证第一帧一定触发切换
    private float _moveSpeed;

    // ---- 快照缓冲 + 延迟插值 ----
    private readonly List<SnapPoint> _buffer = new();
    [Tooltip("插值延迟（秒）：渲染比主机晚这么多，换取平滑。局域网 0.1 即可")]
    [SerializeField] private float interpDelay = 0.1f;
    [Tooltip("快照保留窗口（秒）：超过 delay 后还保留这么久的历史，用于插值区间")]
    [SerializeField] private float bufferWindow = 0.25f;
    [Tooltip("跳变阈值（米）：相邻快照位移超过它直接瞬移（闪避/传送），不被插值成慢飘")]
    [SerializeField] private float teleportThreshold = 3f;
    private bool _firstSnapApplied;

    public string PlayerName { get; private set; }

    /// <summary>
    /// 幽灵化：由 BattleClientRuntime 生成时调用。
    /// source 是克隆 prefab 上的 PlayerController（先读攻击动画名，再销毁模拟组件）。
    /// </summary>
    public void Setup(PlayerController source, string playerName)
    {
        PlayerName = playerName;
        if (source != null && source.comboConfigSO != null && source.comboConfigSO.steps.Length > 0)
            _attackAnimName = source.comboConfigSO.steps[0].animStateName;

        _animator = GetComponent<Animator>();
        if (_animator != null) _animator.applyRootMotion = false;   // 根运动交给插值，不由动画推位移

        // 销毁模拟/输入/反馈组件：幽灵不做逻辑，只做画面。
        // CharacterAnimationEvents 缓存了控制器引用，不销毁会在动画事件触发时报 MissingReference。
        foreach (var c in GetComponentsInChildren<PlayerController>(true)) Destroy(c);
        foreach (var c in GetComponentsInChildren<MoveInputMY>(true)) Destroy(c);
        foreach (var c in GetComponentsInChildren<PlayerInput>(true)) Destroy(c);
        // 注意：PlayerAudio / CharacterVFX / CharacterAnimationEvents 全部保留——
        // 它们是动画事件的接收者（PlayFootSound / PlayWeaponBackSound / ATK / PlayVFX 等），
        // 销毁会导致 "AnimationEvent has no receiver" 刷屏；且它们都不引用已销毁的控制器（安全）。
        // 保留后幽灵走路/攻击还有音效和特效，视觉表现更完整。
        // CharacterController 是 Collider 子类，只销毁 Collider 即可全部覆盖（防重复销毁警告）
        foreach (var c in GetComponentsInChildren<Collider>(true)) Destroy(c);

    }

    /// <summary>收到一帧快照条目：更新目标位置/朝向，并驱动动画（BattleClientRuntime 每帧快照调用）</summary>
    public void ApplySnapshot(BattleSnapshotItem item)
    {
        var pos = new Vector3(item.PosX, item.PosY, item.PosZ);
        var rot = Quaternion.Euler(0f, item.RotY, 0f);

        // 入队 + 裁剪：只保留插值窗口内的历史帧
        _buffer.Add(new SnapPoint { Time = Time.unscaledTime, Pos = pos, Rot = rot });
        while (_buffer.Count > 2 && _buffer[0].Time < Time.unscaledTime - interpDelay - bufferWindow)
            _buffer.RemoveAt(0);

        _moveSpeed = item.MoveSpeed;
        PlayAnim(item.Anim);   // 动画状态用最新快照（50ms 更新一次可接受，位置才走插值）

        // 第一帧直接落位：开局不飞过来，从真实位置开始
        if (!_firstSnapApplied)
        {
            _firstSnapApplied = true;
            transform.position = pos;
            transform.rotation = rot;
            return;
        }

        // 跳变检测：相邻快照位移过大（闪避/传送）→ 直接瞬移，避免插值成慢飘
        if (_buffer.Count >= 2)
        {
            var prev = _buffer[_buffer.Count - 2];
            var curr = _buffer[_buffer.Count - 1];
            if (Vector3.Distance(prev.Pos, curr.Pos) > teleportThreshold)
            {
                transform.position = curr.Pos;
                transform.rotation = curr.Rot;
            }
        }
    }

    private void Update()
    {
        // 延迟插值：取 now - interpDelay 所在区间，前后两帧 Lerp —— 平滑且不追最新（防抖动）
        if (_buffer.Count == 0) return;

        float targetTime = Time.unscaledTime - interpDelay;
        SnapPoint a = _buffer[0];
        SnapPoint b = _buffer[_buffer.Count - 1];

        for (int i = 0; i < _buffer.Count - 1; i++)
        {
            if (_buffer[i].Time <= targetTime && targetTime <= _buffer[i + 1].Time)
            {
                a = _buffer[i];
                b = _buffer[i + 1];
                break;
            }
            if (_buffer[i].Time >= targetTime) break;
        }

        // 缓冲不足（刚开局/断流）：退化用最新帧，保证至少能跟随
        if (b.Time < targetTime)
            a = b;

        float t = Mathf.Clamp01(Mathf.InverseLerp(a.Time, b.Time, targetTime));
        transform.position = Vector3.Lerp(a.Pos, b.Pos, t);
        transform.rotation = Quaternion.Slerp(a.Rot, b.Rot, t);
    }

    /// <summary>动画状态翻译：同状态只刷参数（Movement 持续变化），状态变了才 CrossFade（防止反复重播）</summary>
    private void PlayAnim(BattleAnimState anim)
    {
        if (_animator == null) return;

        if (anim == _lastAnim)
        {
            if (anim == BattleAnimState.Run || anim == BattleAnimState.Sprint)
                _animator.SetFloat("Movement", _moveSpeed);   // 跑步中速度持续变化
            return;
        }
        _lastAnim = anim;

        switch (anim)
        {
            case BattleAnimState.Idle:
                _animator.SetBool("HasInput", false);
                _animator.SetFloat("Movement", 0f);
                break;
            case BattleAnimState.Run:
            case BattleAnimState.Sprint:
                _animator.SetBool("HasInput", true);
                _animator.SetFloat("Movement", _moveSpeed);
                break;
            default:
                string fadeName = BattleAnimMapper.CrossFadeName(anim, _attackAnimName);
                if (!string.IsNullOrEmpty(fadeName))
                    _animator.CrossFadeInFixedTime(fadeName, 0.1f);
                break;
        }
    }
}
