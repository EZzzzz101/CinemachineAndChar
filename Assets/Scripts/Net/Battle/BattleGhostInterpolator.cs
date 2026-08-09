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
    private string _hitAnimName = "BeHit";       // 受击动画名（玩家 BeHit；Boss 用 "Hit"）
    private string _deathAnimName = "Death";     // 死亡动画名（通用）
    private BattleAnimState _lastAnim = (BattleAnimState)0xFF;   // 初始"无状态"，保证第一帧一定触发切换
    private int _lastAnimHash;       // Boss 用：上次切换的 Animator 状态 hash
    private bool _teleportEnabled = true;   // 玩家幽灵：闪避大位移瞬移；Boss：关闭（冲刺也要平滑插值）
    private bool _isBoss;                   // Boss 幽灵：动画参数回放（SpeedX/SpeedY/IsSolo/IsMoving）+ CC 移动
    private CharacterController _cc;        // Boss 幽灵的 CharacterController：走 Move 保持碰撞一致（防穿模）
    private float _moveSpeed;

    // ---- 快照缓冲 + 延迟插值 ----
    private readonly List<SnapPoint> _buffer = new();
    [Tooltip("插值延迟（秒）：渲染比主机晚这么多，换取平滑。局域网 0.1 即可；Boss 用 0.05 减少动作滞后滑步")]
    [SerializeField] private float interpDelay = 0.1f;
    [Tooltip("快照保留窗口（秒）：超过 delay 后还保留这么久的历史，用于插值区间")]
    [SerializeField] private float bufferWindow = 0.25f;
    [Tooltip("跳变阈值（米）：相邻快照位移超过它直接瞬移（闪避/传送），不被插值成慢飘")]
    [SerializeField] private float teleportThreshold = 3f;
    [Tooltip("断流阈值（秒）：快照停更超过它 → 冻结位置，避免向过期目标滑行/恢复时闪现")]
    [SerializeField] private float stallThreshold = 0.15f;
    private bool _firstSnapApplied;
    private float _lastSnapTime = float.MinValue;   // 最后收到快照的时间（断流检测）
    private bool _stalled;                          // 是否正处于断流冻结中
    private float _debugLogTimer;                   // [BossSync] 客户端周期采样日志节流

    // ---- 方案A：Boss 幽灵 root motion 驱动位置，快照只纠偏 ----
    [Tooltip("Boss 纠偏阈值（米）：root motion 位置与快照最新位置偏差超过它才拉回。0.2 够紧又不和动画打架")]
    [SerializeField] private float correctThreshold = 0.2f;
    [Tooltip("Boss 拉回速度（米/秒）：纠偏时不瞬移，按此速度向权威位置靠拢")]
    [SerializeField] private float pullSpeed = 4f;
    private const float GroundSnapSpeed = 2f;   // 贴地吸附（和主机 Test.cs 一致）

    public string PlayerName { get; private set; }

    /// <summary>
    /// 幽灵化：由 BattleClientRuntime 生成时调用。
    /// source 是克隆 prefab 上的 PlayerController（先读攻击动画名，再销毁模拟组件）。
    /// </summary>
    public void Setup(PlayerController source, string playerName)
    {
        _isBoss = false;
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

    /// <summary>
    /// Boss 幽灵化配置：动画名与玩家不同（Hit/Death），位置/插值逻辑复用。
    /// </summary>
    public void SetupAsBoss(string hitAnimName, string deathAnimName)
    {
        _isBoss = true;
        PlayerName = "Boss";
        _hitAnimName = hitAnimName;
        _deathAnimName = deathAnimName;
        _teleportEnabled = false;   // Boss 冲刺不是传送，禁用跳变瞬移 → 不再闪现
        interpDelay = 0.03f;        // Boss 动作位移大，插值离主机更近（配合 60Hz 快照 → 追上根运动轨迹）
        _animator = GetComponent<Animator>();
        _cc = GetComponent<CharacterController>();   // Boss 幽灵保留 CC：root motion 走 Move → 碰撞与主机一致
        // 方案A：Boss 幽灵开 root motion，位置跟动画（和主机 Test.cs 一致）→ 身体不再"飘着滑"。
        // OnAnimatorMove 里 _cc.Move(deltaPosition) 结算，快照只做纠偏（Update 里偏差大才拉回）。
        if (_animator != null) _animator.applyRootMotion = true;
    }

    /// <summary>
    /// Boss 幽灵 root motion 结算（方案A）：位置跟动画走，和主机 Test.cs 一致。
    /// 快照只做纠偏（Update 里偏差大才拉回），平时让动画自己推 → 不滑步。
    /// 玩家幽灵 applyRootMotion=false，Animator 不会调到这里。
    /// </summary>
    private void OnAnimatorMove()
    {
        if (!_isBoss || _animator == null || _cc == null) return;
        _cc.Move(_animator.deltaPosition + Vector3.down * GroundSnapSpeed * Time.deltaTime);
    }

    /// <summary>收到一帧快照条目：更新目标位置/朝向，并驱动动画（BattleClientRuntime 每帧快照调用）</summary>
    public void ApplySnapshot(BattleSnapshotItem item, int tick = 0)
    {
        var pos = new Vector3(item.PosX, item.PosY, item.PosZ);
        var rot = Quaternion.Euler(0f, item.RotY, 0f);

        // 入队 + 裁剪：只保留插值窗口内的历史帧
        _buffer.Add(new SnapPoint { Time = Time.unscaledTime, Pos = pos, Rot = rot });
        while (_buffer.Count > 2 && _buffer[0].Time < Time.unscaledTime - interpDelay - bufferWindow)
            _buffer.RemoveAt(0);

        _lastSnapTime = Time.unscaledTime;   // 断流检测：快照在持续到
        _moveSpeed = item.MoveSpeed;
        // Boss 动画由行为树 Trigger 驱动、状态名不固定，用 hash 直接切同名状态；
        // 玩家走枚举驱动（HasInput/Movement）。
        bool animSwitched = false;
        if (item.AnimHash != 0)
            animSwitched = PlayAnimByHash(item.AnimHash, item.BossNormalizedTime);
        else
            PlayAnim(item.Anim);

        // Boss 额外回放 2D 混合树参数（对峙横移/走位开关）——否则客户端停在 (0,0) 站桩
        if (_isBoss)
        {
            ApplyBossParams(item);

            // [BossSync] 客户端周期采样（0.5s）：和主机 [Host] 同 tick 日志对比位置/相位/参数
            _debugLogTimer -= Time.unscaledDeltaTime;
            if (_debugLogTimer <= 0f)
            {
                _debugLogTimer = 0.5f;
                var st = _animator != null ? _animator.GetCurrentAnimatorStateInfo(0) : default;
                float gap = Vector3.Distance(transform.position, pos);
                Debug.Log($"[BossSync] [Clnt] tick={tick} | pos={pos:F1} " +
                          $"hash={st.shortNameHash} time={st.normalizedTime:F2} " +
                          $"sx={item.BossSpeedX:F2} sy={item.BossSpeedY:F2} " +
                          $"solo={item.BossIsSolo} move={item.BossIsMoving} posGap={gap:F2}m");
            }
        }

        // 第一帧直接落位：开局不飞过来，从真实位置开始
        if (!_firstSnapApplied)
        {
            _firstSnapApplied = true;
            transform.position = pos;
            transform.rotation = rot;
            return;
        }

        // 状态切换/相位大跳：把位置对齐到主机——主机已累积的 root motion 全在快照位置里，
        // 切换瞬间对齐，偏差不会在状态之间累积（root motion 方案的关键，消除跨状态偏移）
        if (_isBoss && animSwitched)
        {
            transform.position = pos;
            transform.rotation = rot;
        }

        // 跳变检测：相邻快照位移过大（闪避/传送）→ 直接瞬移，避免插值成慢飘
        if (_teleportEnabled && _buffer.Count >= 2)
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

        // 断流保护：快照停更超过阈值。
        // 玩家：冻结位置，不向过期目标滑行；Boss：位置由 root motion 继续走，只停纠偏。
        if (_lastSnapTime > 0f && Time.unscaledTime - _lastSnapTime > stallThreshold)
        {
            _stalled = true;
            return;
        }

        // 断流恢复：位移过大 → 直接落位（网络中断期间主机真动了，不慢飘追赶）
        if (_stalled)
        {
            _stalled = false;
            var latest = _buffer[_buffer.Count - 1];
            if (Vector3.Distance(transform.position, latest.Pos) > teleportThreshold)
            {
                transform.position = latest.Pos;
                transform.rotation = latest.Rot;
                return;
            }
        }

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
        Vector3 pos = Vector3.Lerp(a.Pos, b.Pos, t);
        Quaternion rot = Quaternion.Slerp(a.Rot, b.Rot, t);

        if (_isBoss && _cc != null)
        {
            // 方案A：位置由 root motion 驱动（OnAnimatorMove），快照只纠偏——
            // 偏差超过阈值才平滑拉回，平时让动画自己走（不打架、不滑步）
            var latest = _buffer[_buffer.Count - 1];
            float drift = Vector3.Distance(transform.position, latest.Pos);
            if (drift > correctThreshold)
                transform.position = Vector3.MoveTowards(transform.position, latest.Pos, pullSpeed * Time.deltaTime);
        }
        else
        {
            transform.position = pos;
        }
        transform.rotation = rot;
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
                // 受击/死亡动画名可配置（玩家 BeHit/Death；Boss Hit/Death），其余走通用映射
                string fadeName = anim == BattleAnimState.Hit ? _hitAnimName
                                : anim == BattleAnimState.Dead ? _deathAnimName
                                : BattleAnimMapper.CrossFadeName(anim, _attackAnimName);
                if (!string.IsNullOrEmpty(fadeName))
                    _animator.CrossFadeInFixedTime(fadeName, 0.1f);
                break;
        }
    }

    /// <summary>
    /// Boss 动画：直接跳到与主机相同的 Animator 状态 + 相同相位。
    /// 两端同一 Animator Controller → shortNameHash 一致。
    /// 不用防抖、不用 CrossFade——相位由主机 normalizedTime 锁死，
    /// 硬切才能让动画和主机完全同速（否则客户端总是滞后 + 动画/位置对不上滑步）。
    /// 同一状态下相位漂移过大才校正（主机时停/卡顿时动画会停，客户端还在播，需拉回）。
    /// </summary>
    private bool PlayAnimByHash(int hash, float normalizedTime)
    {
        if (_animator == null) return false;

        var state = _animator.GetCurrentAnimatorStateInfo(0);
        if (hash != _lastAnimHash)
        {
            _lastAnimHash = hash;
            _animator.Play(hash, 0, normalizedTime);   // 硬切 + 锁相位（两端从同一进度播）
            // [BossSync] 客户端状态切换：和主机 [Host] 的状态切换日志对比，看动作延迟多少
            Debug.Log($"[BossSync] [Clnt] 状态切换 hash={hash} time={normalizedTime:F2}");
            return true;   // 状态切换 → 调用方把位置对齐到主机（重置跨状态累积的偏差）
        }

        // 同一状态：相位差取最短环绕距离，过大才校正（避免每帧重设造成动画微卡）
        if (state.shortNameHash != hash) return false;
        float diff = Mathf.Abs(state.normalizedTime - normalizedTime);
        diff = Mathf.Min(diff, 1f - diff);
        if (diff > 0.2f)
        {
            _animator.Play(hash, 0, normalizedTime);
            return true;   // 相位大跳（主机时停恢复）→ 同样对齐位置
        }
        return false;
    }

    /// <summary>
    /// Boss 动画参数回放：把主机采样的 2D 混合树参数写进本地 Animator。
    /// 状态切换仍由 PlayAnimByHash 负责（只驱动"持续参数"），避免两个组件抢 Animator。
    /// 关键：SpeedX/SpeedY 是主机已阻尼过的当前值，客户端直接写即可复现对峙横移。
    /// </summary>
    private void ApplyBossParams(BattleSnapshotItem item)
    {
        if (_animator == null) return;
        _animator.SetFloat("SpeedX", item.BossSpeedX);
        _animator.SetFloat("SpeedY", item.BossSpeedY);
        _animator.SetBool("IsSolo", item.BossIsSolo);
        _animator.SetBool("IsMoving", item.BossIsMoving);
    }
}
