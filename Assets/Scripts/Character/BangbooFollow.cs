using UnityEngine;

/// <summary>
/// Bangboo 兔子跟随控制
/// Flower=true → 播奔跑动画（动画自带 root motion 位移）
/// Flower=false → 播特殊待机
/// 脚本只负责朝向，位移由动画驱动（需开启 Animator 的 Apply Root Motion）
/// </summary>
public class BangbooFollow : MonoBehaviour
{
    [Header("跟随目标")]
    [SerializeField] private Transform _target;

    [Header("跟随设置")]
    [SerializeField] private bool _follow = true;
    [Tooltip("玩家离开超过此距离，开始追")]
    [SerializeField] private float _startFollowDistance = 3f;
    [Tooltip("追到此距离内，停下")]
    [SerializeField] private float _stopFollowDistance = 1.5f;
    [SerializeField] private float _rotationSpeed = 10f;

    private Animator _animator;
    private bool _isMoving;
    private static readonly int FlowerParam = Animator.StringToHash("Flower");

    public bool Follow
    {
        get => _follow;
        set => _follow = value;
    }

    void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    void Start()
    {
        // 自动查找玩家
        if (_target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _target = player.transform;
        }
    }

    void Update()
    {
        if (_target == null) return;

        float distance = Vector3.Distance(transform.position, _target.position);

        // 滞后判定：离开用大距离，靠近用小距离，防止边界抖动
        if (!_isMoving && _follow && distance > _startFollowDistance)
        {
            _isMoving = true;
        }
        else if (_isMoving && distance <= _stopFollowDistance)
        {
            _isMoving = false;
        }

        if (_follow && _isMoving)
        {
            _animator.SetBool(FlowerParam, true);
            FaceTarget();
        }
        else
        {
            _animator.SetBool(FlowerParam, false);
        }
    }

    /// <summary>只转向目标，位移交给动画的 root motion</summary>
    private void FaceTarget()
    {
        Vector3 direction = (_target.position - transform.position).normalized;
        direction.y = 0;

        if (direction.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRotation,
                Time.deltaTime * _rotationSpeed
            );
        }
    }

    void OnDrawGizmosSelected()
    {
        // 外圈 = 开始跟随距离，内圈 = 停止跟随距离
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _startFollowDistance);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _stopFollowDistance);
    }
}
