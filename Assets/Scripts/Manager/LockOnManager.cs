using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using SingletonTool;

/// <summary>
/// 锁定管理器 — 双VCam方案
/// 自由相机(POV) ←→ 索敌相机(Composer + TargetGroup)
/// 中键切换，Cinemachine 自动过渡
/// </summary>
public class LockOnManager : Singleton<LockOnManager>
{
    [Header("锁定参数")]
    [SerializeField] private float _lockRange = 15f;
    [SerializeField] private float _lockViewAngle = 60f;
    [SerializeField] private float _loseRange = 20f;
    [SerializeField] private float _loseAngle = 90f;
    [SerializeField] private float _blendTime = 0.25f;

    [Header("引用（可空，运行时会自动查找 / 创建）")]
    [SerializeField] private CinemachineVirtualCamera _freeCam;
    [SerializeField] private CinemachineTargetGroup  _targetGroup;
    [SerializeField] private Transform               _playerTransform;

    [Header("TargetGroup 取景权重")]
    [Tooltip("玩家权重（越大取景点越偏玩家，相机越稳、不飘到两人中点）")]
    [SerializeField] private float _playerGroupWeight = 0.7f;
    [Tooltip("敌人权重")]
    [SerializeField] private float _enemyGroupWeight = 0.3f;

    [Header("索敌相机")]
    [Tooltip("索敌相机与玩家的固定距离（关掉群框后生效；越大视野越平、贴脸时越不俯视）")]
    [SerializeField] private float _lockCamDistance = 4.5f;
    [Tooltip("玩家在画面中的垂直位置(0~1)。偏低=相机略仰，减少俯视感")]
    [SerializeField] private float _lockScreenY = 0.45f;

    // 代码创建的索敌相机
    private CinemachineVirtualCamera _lockOnCam;
    private Transform _playerAimPoint;   // 玩家胸口锚点（没有 CameraBasePoint 时自动创建）

    public LockOnTarget CurrentTarget { get; private set; }
    public bool IsLockedOn => CurrentTarget != null;
    public event System.Action<LockOnTarget> OnLockOnChanged;

    protected override void Awake()
    {
        base.Awake();
    }

    void Start()
    {
        FindReferences();
        CreateOrFindTargetGroup();
        CreateLockOnCam();
        SetGroupTargets(false);

        // 设置过渡曲线：EaseInOut = 先加速再减速
        var brain = FindObjectOfType<CinemachineBrain>();
        if (brain != null)
        {
            brain.m_DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Style.EaseInOut,
                _blendTime
            );
            Debug.Log($"[LockOn] 相机过渡: EaseInOut {_blendTime}s");
        }

        var playerInput = BattleInputLocator.FindLocalPlayerInput();
        if (playerInput != null)
            playerInput.actions["Player/LockOn"].started += OnLockOnInput;
    }

    // ==================== 初始化 ====================

    void FindReferences()
    {
        if (_freeCam == null)
            _freeCam = FindObjectOfType<CinemachineVirtualCamera>();
        if (_playerTransform == null)
            _playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        Debug.Log($"[LockOn] 初始化: freeCam={_freeCam != null} player={_playerTransform != null}");
    }

    /// <summary>
    /// 运行时玩家生成后显式注入（安比 tag 是 Untagged，不能靠 FindGameObjectWithTag 兜底）。
    /// 锁敌相机 Follow 和取景组都依赖这个引用。
    /// </summary>
    public void BindPlayer(Transform player)
    {
        _playerTransform = player;

        // 索敌相机立刻拿到 Follow/LookAt：避免 Follow 为空时 Body 空转导致相机位置退化（贴地/钻地）
        if (_lockOnCam != null)
        {
            _lockOnCam.Follow = GetPlayerAimPoint();
            _lockOnCam.LookAt = _targetGroup != null ? _targetGroup.transform : null;
        }

        SetGroupTargets(IsLockedOn);
        Debug.Log($"[LockOn] 绑定玩家: {player?.name}");
    }

    /// <summary>
    /// 玩家取景锚点：优先找 CameraBasePoint（胸口），没有则在角色根下创建一个胸口锚点。
    /// 出生点模式下角色根在脚底，相机跟根会低头看地、玩家出画。
    /// </summary>
    Transform GetPlayerAimPoint()
    {
        if (_playerTransform == null) return null;

        if (_playerAimPoint != null && _playerAimPoint.parent == _playerTransform)
            return _playerAimPoint;

        foreach (var t in _playerTransform.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "CameraBasePoint")
            {
                _playerAimPoint = t;
                return t;
            }
        }

        var go = new GameObject("CameraBasePoint");
        go.transform.SetParent(_playerTransform, false);
        go.transform.localPosition = new Vector3(0f, 0.8f, 0f);   // 胸口高度
        _playerAimPoint = go.transform;
        return _playerAimPoint;
    }

    void CreateOrFindTargetGroup()
    {
        if (_targetGroup == null)
        {
            _targetGroup = FindObjectOfType<CinemachineTargetGroup>();
            if (_targetGroup == null)
            {
                var go = new GameObject("LockOnTargetGroup");
                DontDestroyOnLoad(go);
                _targetGroup = go.AddComponent<CinemachineTargetGroup>();
                Debug.Log("[LockOn] 自动创建 CinemachineTargetGroup");
            }
        }
    }

    /// <summary>
    /// 克隆自由相机生成索敌相机：
    /// 保留 Body (FramingTransposer) / Lens 等设置，把 Aim 换成 Composer
    /// </summary>
    void CreateLockOnCam()
    {
        if (_lockOnCam != null) return;

        // 独立专用索敌相机：不克隆自由相机，Follow/LookAt 在 Lock 时显式指定，
        // 不依赖自由相机的快照 → 出生点模式下也不会因为 Follow 未绑定而乱飞。
        var holder = new GameObject("LockOnCam");
        holder.transform.SetParent(transform, false);

        _lockOnCam = holder.AddComponent<CinemachineVirtualCamera>();
        _lockOnCam.Priority = 0;
        if (_freeCam != null)
            _lockOnCam.m_Lens.FieldOfView = _freeCam.m_Lens.FieldOfView;

        // 子管线物体（Cinemachine 的 Body/Aim 挂在带 CinemachinePipeline 的子物体上）
        var pipelineGo = new GameObject("cm");
        pipelineGo.transform.SetParent(holder.transform, false);
        pipelineGo.AddComponent<CinemachinePipeline>();

        // Body：FramingTransposer，None 模式 + 固定距离（和之前克隆后的行为一致）
        var body = pipelineGo.AddComponent<CinemachineFramingTransposer>();
        body.m_GroupFramingMode = CinemachineFramingTransposer.FramingMode.None;
        body.m_CameraDistance = _lockCamDistance;   // 更远，视野更平
        body.m_ScreenX = 0.5f;
        body.m_ScreenY = _lockScreenY;              // 玩家放画面偏下，减少俯视感
        body.m_TargetMovementOnly = false;

        // Aim：Composer 自动看向 LookAt（锁定时指向 TargetGroup）
        pipelineGo.AddComponent<CinemachineComposer>();

        // 碰撞避障：忽略玩家(6)/敌人(7)层，墙/地形仍会避
        var camCollider = holder.AddComponent<CinemachineCollider>();
        camCollider.m_CollideAgainst = ~((1 << 6) | (1 << 7));

        Debug.Log("[LockOn] 专用索敌相机创建完成");
    }

    // ==================== 输入 ====================

    void OnLockOnInput(InputAction.CallbackContext ctx)
    {
        Debug.Log("[LockOn] 中键按下");
        ToggleLockOn();
    }

    protected override void OnDestroy()
    {
        var playerInput = BattleInputLocator.FindLocalPlayerInput();
        if (playerInput != null)
            playerInput.actions["Player/LockOn"].started -= OnLockOnInput;
        base.OnDestroy();
    }

    // ==================== 切换逻辑 ====================

    public void ToggleLockOn()
    {
        if (IsLockedOn)
        {
            Debug.Log("[LockOn] 解锁");
            Unlock();
        }
        else
        {
            LockOnTarget nearest = FindNearestTarget();
            if (nearest != null)
                Lock(nearest);
            else
                Debug.Log("[LockOn] 未找到可锁定目标");
        }
    }

    public void Lock(LockOnTarget target)
    {
        CurrentTarget = target;
        SetGroupTargets(true);

        // Follow/LookAt 显式指定（跟胸口锚点，不依赖克隆快照，出生点模式也安全）
        Transform aimPoint = GetPlayerAimPoint();
        _lockOnCam.Follow = aimPoint;
        _lockOnCam.LookAt = _targetGroup.transform;

        // 固定初始机位：玩家身后、略上方、面向敌人（不沿用自由相机当前朝向，避免甩到天上/地板）
        Vector3 toTarget = target.GetLockOnPosition() - _playerTransform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.01f)
            toTarget = _playerTransform.forward;
        Quaternion facing = Quaternion.LookRotation(toTarget.normalized);
        _lockOnCam.transform.rotation = facing;
        _lockOnCam.transform.position = aimPoint.position
                                        - facing * Vector3.forward * _lockCamDistance
                                        + Vector3.up * 2f;

        _lockOnCam.Priority = 20;
        _freeCam.Priority   = 10;

        Debug.Log($"[LockOn] 锁定: {target.name} | Prio切换: FreeCam=10 LockOnCam=20 | 过渡中...");
        OnLockOnChanged?.Invoke(CurrentTarget);
    }

    public void Unlock()
    {
        // 同步 FreeCam 的 POV Axis，而不是复制 Transform：
        // FreeCam 旋转由 CinemachinePOV 内部 Axis 决定，只改 Transform 下一帧会被覆盖 → 绕大圈
        SyncFreeCamFromLockCam();

        _lockOnCam.Priority = 0;
        _freeCam.Priority   = 10;

        Debug.Log("[LockOn] 解锁 | POV Axis 同步到索敌视角 → 0.25s 滑回 POV");
        CurrentTarget = null;
        SetGroupTargets(false);

        OnLockOnChanged?.Invoke(null);
    }

    /// <summary>
    /// 把 LockOnCam 当前观察方向同步给 FreeCam 的 POV Axis。
    /// LockOnCam 旋转由 Composer+TargetGroup 决定，FreeCam 由 POV 内部 Axis 决定，
    /// 控制方式不同 → 要把"观察方向"转换成 POV 的 yaw/pitch，而不是复制 Transform。
    /// </summary>
    void SyncFreeCamFromLockCam()
    {
        var pov = _freeCam.GetCinemachineComponent<CinemachinePOV>();
        if (pov == null) return;

        // 索敌相机当前真实观察方向（Composer 已算出）
        Vector3 fwd = _lockOnCam.transform.forward;

        // POV 朝向 = Quaternion.Euler(pitch, yaw, 0)，forward = (sin yaw·cos pitch, sin pitch, cos yaw·cos pitch)
        float yaw   = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        float pitch = Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg;

        pov.m_HorizontalAxis.Value = yaw;
        pov.m_VerticalAxis.Value = pitch;
    }

    void Update()
    {
        if (!IsLockedOn) return;

        if (ShouldLoseLock())
            Unlock();

        // 每秒 log 一次当前活跃相机
        if (Time.frameCount % 60 == 0)
        {
            var brain = CinemachineCore.Instance.GetActiveBrain(0);
            if (brain != null)
            {
                var active = brain.ActiveVirtualCamera;
                Debug.Log($"[LockOn] 活跃相机: {(active != null ? active.Name : "null")} | IsBlending: {brain.IsBlending}");
            }
        }
    }

    // ==================== TargetGroup ====================

    void SetGroupTargets(bool locked)
    {
        if (_targetGroup == null || _playerTransform == null) return;

        // 组位置用"加权平均"而非包围盒中点：配合权重让取景点偏向玩家，不飘到两人正中间
        _targetGroup.m_PositionMode = CinemachineTargetGroup.PositionMode.GroupAverage;

        if (locked && CurrentTarget != null)
        {
            var targets = new CinemachineTargetGroup.Target[2];
            targets[0].target = GetPlayerAimPoint();   // 胸口锚点，避免低头看脚
            targets[0].weight = _playerGroupWeight;
            targets[0].radius = 0.5f;
            targets[1].target = CurrentTarget.CameraFocusTransform;   // 敌人相机聚焦点（根 + cameraFocusOffset，独立于锁定点）
            targets[1].weight = _enemyGroupWeight;
            targets[1].radius = 0.5f;
            _targetGroup.m_Targets = targets;
        }
        else
        {
            var targets = new CinemachineTargetGroup.Target[1];
            targets[0].target = GetPlayerAimPoint();
            targets[0].weight = _playerGroupWeight;
            targets[0].radius = 0.5f;
            _targetGroup.m_Targets = targets;
        }
    }

    // ==================== 目标搜索（数据源已迁到 TargetManager）====================

    LockOnTarget FindNearestTarget()
    {
        if (_playerTransform == null || _freeCam == null) return null;

        var cam = _freeCam.transform;

        if (!TargetManager.HasInstance)
        {
            Debug.LogWarning("[LockOn] TargetManager 不存在");
            return null;
        }

        var allTargets = TargetManager.Instance.AllTargets;
        Debug.Log($"[LockOn] 搜寻目标: 共 {allTargets.Count} 个");

        LockOnTarget best = null;
        float bestScore = float.MaxValue;

        foreach (var targetable in allTargets)
        {
            if (targetable == null) continue;

            // 必须同时挂 LockOnTarget 才能拿锁定位移点
            var target = targetable.GetComponent<LockOnTarget>();
            if (target == null) continue;

            if (targetable.Team != Team.Enemy)
            {
                Debug.Log($"[LockOn] 跳过 {targetable.name} (Team:{targetable.Team} != Enemy)");
                continue;
            }

            Vector3 toTarget = target.GetLockOnPosition() - cam.position;
            float dist = toTarget.magnitude;
            if (dist > _lockRange) continue;

            float angle = Vector3.Angle(cam.forward, toTarget.normalized);
            if (angle > _lockViewAngle) continue;

            float score = dist + angle * 0.5f;
            if (score < bestScore)
            {
                bestScore = score;
                best = target;
            }
        }
        Debug.Log($"[LockOn] 搜寻结果: {(best != null ? best.name : "无")}");
        return best;
    }

    // ==================== 脱锁 ====================

    bool ShouldLoseLock()
    {
        if (CurrentTarget == null || _playerTransform == null) return true;

        // 只按距离脱锁，不管朝向（锁定后你可以背对敌人到处走）
        Vector3 toTarget = CurrentTarget.GetLockOnPosition() - _playerTransform.position;
        float dist = toTarget.magnitude;
        if (dist > _loseRange) return true;

        return false;
    }
}
