using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 被邀请弹窗 — 收到邀请消息时弹出（由 LobbyUIBridge 常驻预加载，保证一直在监听）。
/// 打开时锁玩家输入 + 显鼠标；接受 → 回执 + 进房；拒绝 → 回执 + 关闭。
/// </summary>
public class BeInvitedView : UIView
{
    [Header("引用（留空自动查找）")]
    [SerializeField] private Button acceptButton;    // 接受
    [SerializeField] private Button rejectButton;    // 拒绝
    [SerializeField] private TextMeshProUGUI inviteText;   // 邀请文案

    private string _inviterName;

    protected override void Awake()
    {
        base.Awake();   // 注册到 UIManager
        AutoFindReferences();

        if (acceptButton != null) acceptButton.onClick.AddListener(OnAcceptClicked);
        if (rejectButton != null) rejectButton.onClick.AddListener(OnRejectClicked);

        // 常驻订阅：预加载后一直在监听，随时能弹
        LobbyClientService.Instance.OnInvited += OnInvited;
    }

    private void OnInvited(string inviterName)
    {
        _inviterName = inviterName;
        if (inviteText != null) inviteText.text = $"{inviterName} 邀请你进入战斗";
        Show();
    }

    public override void Show()
    {
        transform.SetAsLastSibling();   // 置顶：盖住组队界面等下层 UI
        base.Show();
        PlayerInputGate.EnterUI();   // 锁角色移动 + 显鼠标
    }

    private void OnAcceptClicked()
    {
        LobbyClientService.Instance.ReplyInvite(true);
        HideAndRestore();
    }

    private void OnRejectClicked()
    {
        LobbyClientService.Instance.ReplyInvite(false);
        HideAndRestore();
    }

    private void HideAndRestore()
    {
        Hide();
        PlayerInputGate.ExitUI();   // 恢复操控（进房后 TeamUpView 会再 EnterUI）
    }

    private void AutoFindReferences()
    {
        if (inviteText == null)
        {
            // 找一个不在按钮里的 TMP 当邀请文案
            foreach (var t in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (t.GetComponentInParent<Button>() != null) continue;
                inviteText = t;
                break;
            }
        }

        if (acceptButton == null || rejectButton == null)
        {
            foreach (var b in GetComponentsInChildren<Button>(true))
            {
                var label = b.GetComponentInChildren<TMP_Text>(true);
                if (label == null) continue;
                if (acceptButton == null && label.text.Contains("接受")) acceptButton = b;
                if (rejectButton == null && label.text.Contains("拒绝")) rejectButton = b;
            }
        }

        if (acceptButton == null) Debug.LogWarning("[BeInvitedView] 未找到接受按钮（文案含'接受'）");
        if (rejectButton == null) Debug.LogWarning("[BeInvitedView] 未找到拒绝按钮（文案含'拒绝'）");
        if (inviteText == null) Debug.LogWarning("[BeInvitedView] 未找到邀请文案 TMP");
    }

    private void OnDestroy()
    {
        if (acceptButton != null) acceptButton.onClick.RemoveListener(OnAcceptClicked);
        if (rejectButton != null) rejectButton.onClick.RemoveListener(OnRejectClicked);
        if (LobbyClientService.HasInstance)
            LobbyClientService.Instance.OnInvited -= OnInvited;
    }
}
