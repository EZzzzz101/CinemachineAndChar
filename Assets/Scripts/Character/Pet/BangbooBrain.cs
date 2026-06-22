using UnityEngine;

/// <summary>
/// Bangboo 宠物主控 — 协调跟随/聊天/LLM 响应
/// Chat 是叠加态，不覆盖移动逻辑
/// </summary>
public class BangbooBrain : MonoBehaviour
{
    [Header("跟随设置")]
    [SerializeField] private Transform _target;
    [Tooltip("玩家离开超过此距离，开始追")]
    [SerializeField] private float _startFollowDistance = 3f;
    [Tooltip("追到此距离内，停下")]
    [SerializeField] private float _stopFollowDistance = 1.5f;
    [SerializeField] private float _rotationSpeed = 10f;

    private Animator _animator;
    private SpeechBubble _speechBubble;
    private LLMClient _llmClient;

    // 移动状态
    private bool _isMoving;
    private bool _followEnabled = true;

    // Chat 叠加态
    private bool _isChatting;
    private bool _lastPreChatFollow;  // 进入聊天前是否在跟随

    private static readonly int FlowerParam = Animator.StringToHash("Flower");
    private static readonly int ChatParam   = Animator.StringToHash("Chat");

    /// <summary>外部开关跟随（LLM action 调用）</summary>
    public bool FollowEnabled
    {
        get => _followEnabled;
        set => _followEnabled = value;
    }

    /// <summary>当前是否在聊天叠加态</summary>
    public bool IsChatting => _isChatting;

    void Awake()
    {
        _animator      = GetComponent<Animator>();
        _speechBubble  = GetComponentInChildren<SpeechBubble>();
        _llmClient     = GetComponent<LLMClient>();

        if (_llmClient != null)
        {
            _llmClient.OnThinkingStarted += EnterThinkingMode;
            _llmClient.OnResponseReceived += HandleLLMResponse;
        }

        if (_speechBubble != null)
            _speechBubble.OnAutoHidden += ExitChatMode;
    }

    void Start()
    {
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

        // 移动状态滞后判定（聊天期间不打断，让移动自然继续）
        if (!_isChatting)
        {
            if (!_isMoving && _followEnabled && distance > _startFollowDistance)
            {
                _isMoving = true;
                _animator.SetBool(FlowerParam, true);
            }
            else if (_isMoving && (!_followEnabled || distance <= _stopFollowDistance))
            {
                _isMoving = false;
                _animator.SetBool(FlowerParam, false);
            }
        }

        // 跟随中转向目标
        if (_isMoving)
        {
            FaceTarget();
        }
    }

    void OnAnimatorMove()
    {
        transform.position += _animator.deltaPosition;
    }

    /// <summary>只转朝向，位移由动画 root motion 驱动</summary>
    private void FaceTarget()
    {
        Vector3 dir = (_target.position - transform.position).normalized;
        dir.y = 0;
        if (dir.magnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot,
                Time.deltaTime * _rotationSpeed
            );
        }
    }

    // ─────────────── 思考状态 ───────────────

    /// <summary>LLM 请求已发出，显示思考动画</summary>
    private void EnterThinkingMode()
    {
        _speechBubble?.ShowThinking();
    }

    // ─────────────── Chat 叠加态 ───────────────

    /// <summary>进入聊天模式（不打断当前移动状态）</summary>
    public void EnterChatMode(string chatText)
    {
        if (_isChatting)
        {
            // 已在聊天：刷新气泡内容
            _speechBubble?.Show(chatText);
            return;
        }

        _lastPreChatFollow = _isMoving;
        _isChatting = true;
        _animator.SetBool(ChatParam, true);
        _speechBubble?.Show(chatText);
    }

    /// <summary>退出聊天模式，恢复进入前的移动状态</summary>
    public void ExitChatMode()
    {
        if (!_isChatting) return;

        _isChatting = false;
        _animator.SetBool(ChatParam, false);
        _speechBubble?.Hide();

        // 恢复移动状态
        _isMoving = _lastPreChatFollow;
        _animator.SetBool(FlowerParam, _isMoving);
    }

    // ─────────────── LLM 响应处理 ───────────────

    private void HandleLLMResponse(bool success, string rawContent)
    {
        // 无论结果如何，先停止思考动画
        _speechBubble?.StopThinking();

        if (!success)
        {
            Debug.LogError($"[BangbooBrain] LLM 请求失败: {rawContent}");
            return;
        }

        if (!ResponseParser.TryParse(rawContent, out string type, out string content, out string reply))
        {
            Debug.LogWarning($"[BangbooBrain] 解析 LLM 响应失败: {rawContent}");
            return;
        }

        Debug.Log($"[BangbooBrain] LLM → type={type}, content={content}, reply={reply}");

        // 记录对话历史
        if (type == "chat")
        {
            PromptBuilder.CompleteHistory(content);
        }
        else if (!string.IsNullOrEmpty(reply))
        {
            PromptBuilder.CompleteHistory(reply);
        }

        switch (type)
        {
            case "action":
                HandleAction(content, reply);
                break;
            case "chat":
                EnterChatMode(content);
                break;
        }
    }

    private void HandleAction(string action, string reply)
    {
        switch (action.ToLower())
        {
            case "stop":
                _followEnabled = false;
                break;
            case "follow":
                _followEnabled = true;
                break;
        }

        // 显示 AI 生成的口语回复
        if (!string.IsNullOrEmpty(reply))
        {
            _speechBubble?.Show(reply);
        }
    }

    void OnDestroy()
    {
        if (_llmClient != null)
        {
            _llmClient.OnThinkingStarted -= EnterThinkingMode;
            _llmClient.OnResponseReceived -= HandleLLMResponse;
        }
        if (_speechBubble != null)
            _speechBubble.OnAutoHidden -= ExitChatMode;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _startFollowDistance);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _stopFollowDistance);
    }
}
