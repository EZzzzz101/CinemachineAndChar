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
    [SerializeField] private string _enemyTag = "Enemy";
    [SerializeField] private float _blendTime = 0.25f;

    [Header("引用（可空，运行时会自动查找 / 创建）")]
    [SerializeField] private CinemachineVirtualCamera _freeCam;
    [SerializeField] private CinemachineTargetGroup  _targetGroup;
    [SerializeField] private Transform               _playerTransform;

    // 代码创建的索敌相机
    private CinemachineVirtualCamera _lockOnCam;

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

        var playerInput = FindObjectOfType<PlayerInput>();
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
        if (_freeCam == null) return;

        // 克隆
        _lockOnCam = Instantiate(_freeCam, _freeCam.transform.parent);
        _lockOnCam.name = "LockOnCam";

        // 清理多余组件
        var inputProvider = _lockOnCam.GetComponent<CinemachineInputProvider>();
        if (inputProvider != null) Destroy(inputProvider);

        // POV → Composer（只偏头看中点，不强制推拉距离）
        _lockOnCam.DestroyCinemachineComponent<CinemachinePOV>();
        _lockOnCam.AddCinemachineComponent<CinemachineComposer>();

        // 指向 TargetGroup
        _lockOnCam.LookAt = _targetGroup.transform;

        // 初始隐藏（优先级低于自由相机）
        _lockOnCam.Priority = 0;

        Debug.Log("[LockOn] 索敌相机创建完成");
    }

    // ==================== 输入 ====================

    void OnLockOnInput(InputAction.CallbackContext ctx)
    {
        Debug.Log("[LockOn] 中键按下");
        ToggleLockOn();
    }

    protected override void OnDestroy()
    {
        var playerInput = FindObjectOfType<PlayerInput>();
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

        // 从自由相机当前朝向开始过渡（避免跳变）
        _lockOnCam.transform.position = _freeCam.transform.position;
        _lockOnCam.transform.rotation = _freeCam.transform.rotation;
        _lockOnCam.Priority = 20;
        _freeCam.Priority   = 10;

        Debug.Log($"[LockOn] 锁定: {target.name} | Prio切换: FreeCam=10 LockOnCam=20 | 过渡中...");
        OnLockOnChanged?.Invoke(CurrentTarget);
    }

    public void Unlock()
    {
        // 把 FreeCam 拉到 LockOnCam 当前位置/朝向，过渡从同一点出发
        _freeCam.transform.SetPositionAndRotation(
            _lockOnCam.transform.position,
            _lockOnCam.transform.rotation
        );

        _lockOnCam.Priority = 0;
        _freeCam.Priority   = 10;

        Debug.Log("[LockOn] 解锁 | FreeCam 瞬移到索敌位 → 0.25s 滑回 POV");
        CurrentTarget = null;
        SetGroupTargets(false);

        OnLockOnChanged?.Invoke(null);
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

        if (locked && CurrentTarget != null)
        {
            var targets = new CinemachineTargetGroup.Target[2];
            targets[0].target = _playerTransform;
            targets[0].weight = 1f;
            targets[0].radius = 0.5f;
            targets[1].target = CurrentTarget.transform;
            targets[1].weight = 1f;
            targets[1].radius = 0.5f;
            _targetGroup.m_Targets = targets;
        }
        else
        {
            var targets = new CinemachineTargetGroup.Target[1];
            targets[0].target = _playerTransform;
            targets[0].weight = 1f;
            targets[0].radius = 0.5f;
            _targetGroup.m_Targets = targets;
        }
    }

    // ==================== 目标搜索 ====================

    LockOnTarget FindNearestTarget()
    {
        if (_playerTransform == null || _freeCam == null) return null;

        // 从相机位置和朝向检测（屏幕中心法）
        var cam = _freeCam.transform;
        var allTargets = LockOnTarget.ActiveTargets;
        Debug.Log($"[LockOn] 搜寻目标: 共 {allTargets.Count} 个");

        LockOnTarget best = null;
        float bestScore = float.MaxValue;

        foreach (var target in allTargets)
        {
            if (!target.CompareTag(_enemyTag))
            {
                Debug.Log($"[LockOn] 跳过 {target.name} (tag:{target.tag} ≠ {_enemyTag})");
                continue;
            }

            Vector3 toTarget = target.GetLockOnPosition() - cam.position;
            float dist = toTarget.magnitude;
            if (dist > _lockRange) continue;

            // 相机视野内？（屏幕中心优先）
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
