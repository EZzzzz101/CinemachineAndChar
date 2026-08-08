using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// 大厅客户端 UI（测试版）— 运行时构建界面，不依赖预制体。
/// 功能：输入名字连接大厅 → 搜索玩家 → 邀请 → 被邀请弹窗（接受/拒绝）→ 显示进房信息。
/// 挂到客户端场景的空物体上即可。
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("大厅服务器")]
    [SerializeField] private string serverIp = "127.0.0.1";
    [SerializeField] private int serverPort = 7777;

    [Header("字体")]
    [Tooltip("中文字体（留空自动从 Resources/Fonts 加载 SmileySans）")]
    [SerializeField] private TMP_FontAsset lobbyFont;

    private LobbyClient _client;

    private TMP_InputField _nameInput;
    private TMP_InputField _searchInput;
    private TextMeshProUGUI _statusText;
    private TextMeshProUGUI _searchResultText;
    private TextMeshProUGUI _roomText;
    private GameObject _loginPanel;
    private GameObject _lobbyPanel;
    private GameObject _invitePanel;
    private TextMeshProUGUI _inviteText;

    private void Start()
    {
        EnsureEventSystem();
        BuildUI();
        SetStatus("输入名字，点击「连接大厅」");
    }

    private void Update()
    {
        _client?.Poll();
    }

    // ================= 客户端事件 → UI =================

    private void OnRegisterResult(string reason)
    {
        SetStatus($"注册结果：{reason}");
        if (_client.Registered)
        {
            _loginPanel.SetActive(false);
            _lobbyPanel.SetActive(true);
        }
    }

    private void OnSearchResult(bool found, string name)
    {
        _searchResultText.text = found ? $"{name} 在线，可以邀请" : $"{name} 不在线";
    }

    private void OnInvited(string inviterName)
    {
        _inviteText.text = $"{inviterName} 邀请你进入战斗";
        _invitePanel.SetActive(true);
    }

    private void OnJoinedRoom(string hostName, string guestName, string hostIp, int hostPort, int roomId)
    {
        _invitePanel.SetActive(false);
        _roomText.text = $"已进房间 {roomId}：{hostName} + {guestName}，战斗地址 {hostIp}:{hostPort}（下一课开战）";
    }

    private void OnInviteResult(bool accepted, string reason)
    {
        SetStatus(accepted ? $"邀请成功：{reason}" : $"邀请失败：{reason}");
    }

    private void OnError(string msg)
    {
        SetStatus("错误：" + msg);
    }

    // ================= 按钮 =================

    private void OnConnectClicked()
    {
        var name = _nameInput.text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            SetStatus("名字不能为空");
            return;
        }

        _client = new LobbyClient();
        _client.OnRegisterResult += OnRegisterResult;
        _client.OnSearchResult += OnSearchResult;
        _client.OnInvited += OnInvited;
        _client.OnJoinedRoom += OnJoinedRoom;
        _client.OnInviteResult += OnInviteResult;
        _client.OnError += OnError;
        _client.OnDisconnected += OnDisconnected;
        _client.Connect(serverIp, serverPort, name);
        SetStatus("连接中...");
    }

    private void OnDisconnected()
    {
        SetStatus("与服务器断开连接");
        _lobbyPanel?.SetActive(false);
    }

    private void OnSearchClicked()
    {
        var keyword = _searchInput.text.Trim();
        if (string.IsNullOrEmpty(keyword)) return;
        _searchResultText.text = "搜索中...";
        _client?.Search(keyword);
    }

    private void OnInviteClicked()
    {
        var target = _searchInput.text.Trim();
        if (string.IsNullOrEmpty(target)) return;
        _client?.Invite(target);
        SetStatus($"已向 {target} 发出邀请，等对方确认");
    }

    private void OnAcceptClicked()
    {
        _invitePanel.SetActive(false);
        _client?.ReplyInvite(true);
    }

    private void OnRejectClicked()
    {
        _invitePanel.SetActive(false);
        _client?.ReplyInvite(false);
    }

    private void SetStatus(string text)
    {
        if (_statusText != null) _statusText.text = text;
    }

    private void OnDestroy()
    {
        _client?.Disconnect();
    }

    // ================= 运行时构建 UI =================

    private void BuildUI()
    {
        // 中文字体：优先 Inspector 引用，否则从 Resources 加载（SmileySans，支持中文）
        if (lobbyFont == null)
            lobbyFont = Resources.Load<TMP_FontAsset>("Fonts/SmileySans-Oblique SDF");
        if (lobbyFont == null)
            Debug.LogWarning("[LobbyUI] 没找到中文字体，中文可能显示为方框");

        var canvasGo = new GameObject("LobbyCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 背景容器（纵向自动排列）
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.35f, 0.2f);
        rt.anchorMax = new Vector2(0.65f, 0.8f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0, 0, 0, 0.75f);
        var vlg = panel.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 20, 20);
        vlg.spacing = 10;
        vlg.childAlignment = TextAnchor.UpperCenter;

        MakeText(panel.transform, "联机大厅（测试）", 32, Color.white);

        // 登录区
        _loginPanel = MakeArea(panel.transform, "LoginArea");
        _nameInput = MakeInput(MakeRow(_loginPanel.transform), "玩家名字");
        MakeButton(MakeRow(_loginPanel.transform), "连接大厅", OnConnectClicked);

        // 状态
        _statusText = MakeText(panel.transform, "", 20, Color.cyan);

        // 大厅区（注册成功后才显示）
        _lobbyPanel = MakeArea(panel.transform, "LobbyArea");
        _lobbyPanel.SetActive(false);
        _searchInput = MakeInput(MakeRow(_lobbyPanel.transform), "要邀请的玩家名");
        MakeButton(MakeRow(_lobbyPanel.transform), "搜索", OnSearchClicked);
        _searchResultText = MakeText(_lobbyPanel.transform, "", 20, Color.yellow);
        MakeButton(_lobbyPanel.transform, "邀请该玩家", OnInviteClicked);
        _roomText = MakeText(_lobbyPanel.transform, "", 20, Color.green);

        // 邀请弹窗（盖在中间，初始隐藏）
        _invitePanel = new GameObject("InvitePanel", typeof(RectTransform), typeof(Image));
        _invitePanel.transform.SetParent(canvasGo.transform, false);
        var invRt = _invitePanel.GetComponent<RectTransform>();
        invRt.anchorMin = new Vector2(0.4f, 0.45f);
        invRt.anchorMax = new Vector2(0.6f, 0.55f);
        invRt.offsetMin = Vector2.zero;
        invRt.offsetMax = Vector2.zero;
        _invitePanel.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.2f, 0.95f);
        _inviteText = MakeText(_invitePanel.transform, "", 22, Color.white);
        MakeButton(MakeRow(_invitePanel.transform), "接受", OnAcceptClicked);
        MakeButton(MakeRow(_invitePanel.transform), "拒绝", OnRejectClicked);
        _invitePanel.SetActive(false);
    }

    private GameObject MakeArea(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
        go.transform.SetParent(parent, false);
        go.GetComponent<VerticalLayoutGroup>().spacing = 8;
        return go;
    }

    private Transform MakeRow(Transform parent)
    {
        var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(parent, false);
        go.GetComponent<HorizontalLayoutGroup>().spacing = 8;
        go.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        return go.transform;
    }

    private TextMeshProUGUI MakeText(Transform parent, string content, int size, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        if (lobbyFont != null) t.font = lobbyFont;
        return t;
    }

    private TMP_InputField MakeInput(Transform parent, string placeholder)
    {
        var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        go.transform.SetParent(parent, false);
        var input = go.GetComponent<TMP_InputField>();
        go.GetComponent<Image>().color = new Color(1, 1, 1, 0.9f);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 40);

        var textArea = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        textArea.transform.SetParent(go.transform, false);
        Stretch(textArea.GetComponent<RectTransform>(), 4);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(textArea.transform, false);
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.fontSize = 22;
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        if (lobbyFont != null) text.font = lobbyFont;
        input.textComponent = text;
        input.textViewport = textArea.GetComponent<RectTransform>();
        Stretch(textGo.GetComponent<RectTransform>(), 0);

        var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        phGo.transform.SetParent(textArea.transform, false);
        var ph = phGo.GetComponent<TextMeshProUGUI>();
        ph.text = placeholder;
        ph.fontSize = 22;
        ph.color = new Color(0, 0, 0, 0.35f);
        ph.alignment = TextAlignmentOptions.MidlineLeft;
        if (lobbyFont != null) ph.font = lobbyFont;
        input.placeholder = ph;
        Stretch(phGo.GetComponent<RectTransform>(), 0);
        return input;
    }

    private Button MakeButton(Transform parent, string label, UnityAction onClick)
    {
        var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.2f, 0.5f, 0.9f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 40);
        MakeText(go.transform, label, 22, Color.white);
        return btn;
    }

    private static void Stretch(RectTransform rt, float margin)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margin, margin);
        rt.offsetMax = new Vector2(-margin, -margin);
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
