using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 屏幕底部输入框 UI — 全自动，不需要手动拖引用
/// Enter 唤出/发送 · Esc 退出 · 鼠标自动显隐 · 只禁用移动攻击，保留视角
/// </summary>
public class ChatInputUI : MonoBehaviour
{
    private TMP_InputField _inputField;
    private Button _sendButton;
    private CanvasGroup _canvasGroup;
    private bool _isVisible;

    // 运行时自动查找
    private LLMClient _llmClient;
    private BangbooBrain _bangbooBrain;
    private PlayerInput _playerInput;
    private SpeechBubble _playerBubble;
    private InputAction _moveAction, _fireAction, _dashAction;

    void Awake()
    {
        _inputField = GetComponentInChildren<TMP_InputField>();
        _sendButton = GetComponentInChildren<Button>();

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (_sendButton != null)
            _sendButton.onClick.AddListener(Send);

        if (_inputField != null)
        {
            _inputField.lineType = TMP_InputField.LineType.SingleLine;
            _inputField.onSubmit.AddListener(_ => Send());
        }

        Hide();
    }

    void Start()
    {
        // 自动查找所有外部引用
        _llmClient      = FindObjectOfType<LLMClient>();
        _bangbooBrain   = FindObjectOfType<BangbooBrain>();
        _playerInput    = FindObjectOfType<PlayerInput>();

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _playerBubble = player.GetComponentInChildren<SpeechBubble>();

        // 缓存需要禁用的动作（只禁用移动和攻击，保留 CameraLook）
        if (_playerInput != null)
        {
            _moveAction = _playerInput.actions.FindAction("Move");
            _fireAction = _playerInput.actions.FindAction("Fire");
            _dashAction = _playerInput.actions.FindAction("Dash");
        }
    }

    void Update()
    {
        if (_isVisible)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Hide();
                _bangbooBrain?.ExitChatMode();
                return;
            }

            // 输入框失焦时按 Enter → 重新聚焦
            if ((Keyboard.current.enterKey.wasPressedThisFrame ||
                 Keyboard.current.numpadEnterKey.wasPressedThisFrame) &&
                _inputField != null && !_inputField.isFocused)
            {
                _inputField.Select();
                _inputField.ActivateInputField();
            }
        }
        else
        {
            if (Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                Show();
            }
        }
    }

    private void Show()
    {
        _isVisible = true;
        _canvasGroup.alpha = 1f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 只禁用移动/攻击，保留 CameraLook → 视角可转
        _moveAction?.Disable();
        _fireAction?.Disable();
        _dashAction?.Disable();

        _inputField?.Select();
        _inputField?.ActivateInputField();
    }

    private void Hide()
    {
        _isVisible = false;
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        _moveAction?.Enable();
        _fireAction?.Enable();
        _dashAction?.Enable();

        if (_inputField != null) _inputField.text = "";
    }

    private void Send()
    {
        if (_inputField == null || _llmClient == null) return;

        string text = _inputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        _inputField.text = "";

        _playerBubble?.Show(text);
        _llmClient.SendRequest(text);

        _inputField.Select();
        _inputField.ActivateInputField();
    }

    void OnDestroy()
    {
        if (_sendButton != null) _sendButton.onClick.RemoveListener(Send);
    }
}
