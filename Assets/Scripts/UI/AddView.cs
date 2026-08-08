using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 添加好友（搜索玩家 ID）弹窗 — 模态：
/// 打开时全屏遮挡层让下层界面失焦（点不到组队界面）；右上角 X 关闭回到上一层；
/// 输入玩家 ID 点"邀请进入"→ 走联机预留接口，关闭弹窗。
/// </summary>
public class AddView : UIView
{
    [Header("引用（留空自动查找）")]
    [SerializeField] private Button closeButton;     // 右上角 X
    [SerializeField] private Button inviteButton;    // 邀请进入
    [SerializeField] private TMP_InputField idInput; // 玩家 ID 输入框

    private GameObject _modalBlocker;

    public bool IsOpen => gameObject.activeSelf;

    protected override void Awake()
    {
        base.Awake();   // 注册到 UIManager

        AutoFindReferences();
        CreateModalBlocker();

        if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
        if (inviteButton != null) inviteButton.onClick.AddListener(OnInviteClicked);
    }

    public override void Show()
    {
        base.Show();
        if (_modalBlocker != null) _modalBlocker.SetActive(true);
    }

    public override void Hide()
    {
        base.Hide();
        if (_modalBlocker != null) _modalBlocker.SetActive(false);
    }

    private void Update()
    {
        // ESC 关闭弹窗，返回上一层（组队界面）
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            OnCloseClicked();
    }

    private void AutoFindReferences()
    {
        if (idInput == null)
            idInput = GetComponentInChildren<TMP_InputField>(true);

        var buttons = GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            var label = b.GetComponentInChildren<TMP_Text>(true);
            if (inviteButton == null && label != null && label.text.Contains("邀请"))
            {
                inviteButton = b;
                continue;
            }

            if (closeButton == null && (b.name.Contains("DE") || b.name.Contains("Close") || b.name.Contains("X")))
                closeButton = b;
        }

        // 兜底：关闭按钮 = 未识别按钮里面积最小的那个（X 通常是小方块）
        if (closeButton == null)
        {
            Button fallback = null;
            float minArea = float.MaxValue;
            foreach (var b in buttons)
            {
                if (b == inviteButton) continue;
                var rt = b.GetComponent<RectTransform>();
                if (rt == null) continue;
                float area = rt.sizeDelta.x * rt.sizeDelta.y;
                if (area < minArea)
                {
                    minArea = area;
                    fallback = b;
                }
            }
            closeButton = fallback;
        }

        if (closeButton == null) Debug.LogWarning("[AddView] 未找到关闭按钮，请检查预制体");
        if (inviteButton == null) Debug.LogWarning("[AddView] 未找到邀请按钮，请检查预制体");
        if (idInput == null) Debug.LogWarning("[AddView] 未找到玩家ID输入框，请检查预制体");
    }

    /// <summary>全屏遮挡层：让下层界面失焦（点不到组队界面的按钮）</summary>
    private void CreateModalBlocker()
    {
        if (_modalBlocker != null) return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("ModalBlocker");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rt.SetSiblingIndex(transform.GetSiblingIndex());   // 插到弹窗前面，面板在它上层渲染
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.3f);   // 半透明遮罩，透明度可自行调整
        img.raycastTarget = true;

        _modalBlocker = go;
    }

    private void OnCloseClicked()
    {
        Hide();   // 关闭弹窗 + 隐藏遮挡层 → 焦点回到组队界面
    }

    private void OnInviteClicked()
    {
        string playerId = idInput != null ? idInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(playerId))
        {
            Debug.Log("[AddView] 请输入玩家ID");
            return;
        }

        if (IsPlayerOnline(playerId))
            TryInvitePlayer(playerId);
        else
            Debug.Log($"[AddView] 玩家 {playerId} 不在线");

        Hide();   // 点击邀请后弹窗消失，回到组队界面
    }

    /// <summary>联机预留：M7 会话/房间时接真实在线状态（本地阶段先默认在线）</summary>
    private bool IsPlayerOnline(string playerId)
    {
        // TODO 联机：查好友列表/房间在线状态
        return true;
    }

    /// <summary>联机预留接口：真正发送邀请在这里实现（本地阶段只打日志）</summary>
    protected virtual void TryInvitePlayer(string playerId)
    {
        // TODO 联机：向玩家发送邀请 → 成功后组队界面槽位刷出对方立绘
        Debug.Log($"[AddView] 预留联机接口：向玩家 {playerId} 发送邀请");
    }

    private void OnDestroy()
    {
        if (_modalBlocker != null) Destroy(_modalBlocker);
        if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
        if (inviteButton != null) inviteButton.onClick.RemoveListener(OnInviteClicked);
    }
}
