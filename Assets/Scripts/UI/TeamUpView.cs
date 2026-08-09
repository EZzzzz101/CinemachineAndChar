using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

/// <summary>
/// 组队界面 
/// 打开时：鼠标交给 Canvas、冻结角色操控（PlayerInputGate.EnterUI）
/// 关闭时（开始战斗 / ESC）：恢复操控并隐藏鼠标
/// </summary>
public class TeamUpView : UIView
{
    [Header("邀请好友")]
    [SerializeField] private List<Button> AddButtons;

    [Header("开始战斗")]
    [SerializeField] private Button FightButton;

    [Tooltip("点击开始战斗后加载的场景名（单人进战斗）")]
    [SerializeField] private string nextSceneName = "Main";

    [Header("游历家园")]
    [Tooltip("游历家园按钮：联机进六分街（房主广播 RoomStart，双方一起进）")]
    [SerializeField] private Button HomeButton;
    [Tooltip("游历家园加载的场景名")]
    [SerializeField] private string homeSceneName = "SixthStreet";

    [Header("本地玩家立绘（单机一号位）")]
    [Tooltip("拖入立绘 Sprite；留空时尝试 Resources/UI/Portraits/Player")]
    [SerializeField] private Sprite localPortrait;

    private Image _slot1Portrait;
    private readonly HashSet<string> _members = new();

    protected override void Awake()
    {
        base.Awake();                   // 注册到 UIManager（_views 缓存）

        // 序列化列表没接的话，按名字/文案自动找加号按钮和开始战斗按钮
        AutoFindButtons();

        foreach (var button in AddButtons)
        {
            if (button != null)
            {
                button.onClick.AddListener(OnAddButtonClicked);
                var rt = button.GetComponent<RectTransform>();
                if (rt != null && rt.sizeDelta == Vector2.zero)
                    Debug.LogWarning($"[TeamUpView] 加号按钮 {button.name} 尺寸为 0，没有点击区域，请检查预制体");
            }
        }
        if (FightButton != null)
            FightButton.onClick.AddListener(OnFightButtonClicked);
        if (HomeButton != null)
            HomeButton.onClick.AddListener(OnHomeButtonClicked);
        else
            AutoFindHomeButton();

        // 联机房间：收到"房主开始战斗"广播 → 一起进战斗场景
        if (LobbyClientService.HasInstance)
            LobbyClientService.Instance.OnRoomStart += OnRoomStart;

        PlayerInputGate.EnterUI();      // 首次实例化即锁输入 + 显鼠标
        ApplyLocalPlayerPortrait();     // 单机进入：一号位刷本地立绘
    }

    private void AutoFindButtons()
    {
        if (AddButtons == null || AddButtons.Count == 0)
        {
            AddButtons = new List<Button>();
            foreach (var b in GetComponentsInChildren<Button>(true))
            {
                var label = b.GetComponentInChildren<TMP_Text>(true);
                bool isPlus = b.name.Contains("Add") || b.name.Contains("加")
                              || (label != null && label.text.Trim() == "+");
                if (isPlus) AddButtons.Add(b);
            }
            if (AddButtons.Count > 0)
                Debug.Log($"[TeamUpView] 自动找到 {AddButtons.Count} 个加号按钮");
        }

        if (FightButton == null)
        {
            foreach (var b in GetComponentsInChildren<Button>(true))
            {
                if (AddButtons != null && AddButtons.Contains(b)) continue;
                var label = b.GetComponentInChildren<TMP_Text>(true);
                if (label != null && (label.text.Contains("进入") || label.text.Contains("战斗") || label.text.Contains("开始")))
                {
                    FightButton = b;
                    break;
                }
            }
        }
    }

    /// <summary>自动找"游历家园"按钮（按名字/文案，如"游历"/"家园"）</summary>
    private void AutoFindHomeButton()
    {
        foreach (var b in GetComponentsInChildren<Button>(true))
        {
            var label = b.GetComponentInChildren<TMP_Text>(true);
            bool isHome = b.name.Contains("游历") || b.name.Contains("家园")
                          || (label != null && (label.text.Contains("游历") || label.text.Contains("家园")));
            if (isHome)
            {
                HomeButton = b;
                HomeButton.onClick.AddListener(OnHomeButtonClicked);
                Debug.Log($"[TeamUpView] 自动找到游历家园按钮：{b.name}");
                break;
            }
        }
    }

    public override void Show()
    {
        transform.SetAsLastSibling();   // 重新打开时置顶
        base.Show();
        PlayerInputGate.EnterUI();      // 缓存复用再次打开时，重新锁输入 + 显鼠标
        ApplyLocalPlayerPortrait();
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;

        // 有模态弹窗（AddView）打开时，ESC 先关弹窗，不关组队界面
        var addView = UIManager.Instance.Get<AddView>();
        if (addView != null && addView.IsOpen) return;

        // ESC 退出组队界面，回到六分街继续操控
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseLobby();
    }

    /// <summary>单机进入时一号位刷出本地玩家立绘（联机后由槽位系统接管）</summary>
    private async void ApplyLocalPlayerPortrait()
    {
        if (_slot1Portrait == null)
        {
            // 找到一号位槽位容器（名为 head，背景 Image 和加号按钮都挂在它下面）
            foreach (var img in GetComponentsInChildren<Image>(true))
            {
                if (img.name != "head") continue;   // head(1)/(2)/(3) 是其他槽位

                // 不改背景图（会被加号盖住），而是在槽位最上层生成一张立绘 Image
                var portraitGo = new GameObject("LocalPortrait");
                var rt = portraitGo.AddComponent<RectTransform>();
                rt.SetParent(img.transform, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.SetAsLastSibling();   // 盖在加号等元素上面

                var portraitImage = portraitGo.AddComponent<Image>();
                portraitImage.raycastTarget = false;   // 纯展示，不挡下层点击
                _slot1Portrait = portraitImage;
                break;
            }
        }
        if (_slot1Portrait == null) return;

        var sprite = localPortrait != null ? localPortrait : await ResourceManager.Instance.LoadAsync<Sprite>("UI/Portraits/Player");
        if (sprite != null)
            _slot1Portrait.sprite = sprite;
        else
            Debug.LogWarning("[TeamUpView] 未配置本地玩家立绘：拖 localPortrait 或放 Assets/GameAssets/UI/Portraits/Player");
    }

    /// <summary>把一名成员放进下一个空槽位（最多 4 人）；返回是否放得下</summary>
    public bool AddMember(string playerName)
    {
        if (string.IsNullOrEmpty(playerName)) return false;
        if (!_members.Add(playerName)) return true;   // 已在房间，幂等
        if (_members.Count > 4)
        {
            _members.Remove(playerName);
            Debug.LogWarning("[TeamUpView] 房间已满（最多 4 人）");
            return false;
        }

        foreach (var slot in GetComponentsInChildren<Image>(true))
        {
            // 槽位容器：head / head (1) / head (2) / head (3)
            if (slot.name != "head" && !slot.name.StartsWith("head (")) continue;
            if (slot.transform.Find("MemberPortrait") != null) continue; // 已被成员占
            if (slot.transform.Find("LocalPortrait") != null) continue; // 一号位是本地玩家

            CreateMemberSlot(slot.transform, playerName);
            return true;
        }

        _members.Remove(playerName);
        Debug.LogWarning("[TeamUpView] 没有可用槽位");
        return false;
    }

    /// <summary>在槽位里生成：占位立绘 + 名字</summary>
    private void CreateMemberSlot(Transform slot, string playerName)
    {
        // 立绘占位（正式做可以按玩家名拉头像；先用本地立绘顶替）
        var portraitGo = new GameObject("MemberPortrait", typeof(RectTransform), typeof(Image));
        var prt = portraitGo.GetComponent<RectTransform>();
        prt.SetParent(slot, false);
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;
        prt.SetAsLastSibling();
        var img = portraitGo.GetComponent<Image>();
        img.raycastTarget = false;
        if (localPortrait != null) img.sprite = localPortrait;
        else img.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);

    }

    private void CloseLobby()
    {
        PlayerInputGate.ExitUI();       // 恢复操控 + 隐藏鼠标
        gameObject.SetActive(false);
    }

    // 邀请好友界面
    private void OnAddButtonClicked()
    {
        Debug.Log("[TeamUpView] 点击邀请好友");
        UIManager.Instance.Open<AddView>();
    }

    // 跳转场景
    private void OnFightButtonClicked()
    {
        // 开始战斗：各点各的，单人进 Main（不再联机广播）
        PlayerInputGate.ExitUI();
        gameObject.SetActive(false);
        Debug.Log($"[BattleFlow] 开始战斗：单人加载 {nextSceneName}");
        SceneLoader.Instance.LoadScene(nextSceneName);
    }

    /// <summary>游历家园：房主广播 RoomStart + 自己进六分街；客人等广播（OnRoomStart）</summary>
    private void OnHomeButtonClicked()
    {
        PlayerInputGate.ExitUI();
        gameObject.SetActive(false);
        Debug.Log($"[BattleFlow] 游历家园：FromLobby={BattleSessionState.FromLobby} IsHost={BattleSessionState.IsHost}");

        if (BattleSessionState.FromLobby && !BattleSessionState.IsHost)
        {
            Debug.Log("[TeamUpView] 你是客人，等待房主游历家园广播");
            return;
        }
        if (BattleSessionState.FromLobby && LobbyClientService.HasInstance && LobbyClientService.Instance.Registered)
            LobbyClientService.Instance.RequestStartBattle();   // 房主：请大厅通知全房间一起游历

        SceneLoader.Instance.LoadScene(homeSceneName);
    }

    /// <summary>大厅广播"游历家园"：客人收到后一起进六分街</summary>
    private void OnRoomStart(int roomId)
    {
        Debug.Log($"[TeamUpView] 收到游历家园广播（房间 {roomId}），进入六分街");
        PlayerInputGate.ExitUI();
        gameObject.SetActive(false);
        SceneLoader.Instance.LoadScene(homeSceneName);
    }

    private void OnDestroy()
    {
        if (LobbyClientService.HasInstance)
            LobbyClientService.Instance.OnRoomStart -= OnRoomStart;
        if (FightButton != null)
            FightButton.onClick.RemoveListener(OnFightButtonClicked);
        if (HomeButton != null)
            HomeButton.onClick.RemoveListener(OnHomeButtonClicked);
        foreach (var button in AddButtons)
        {
            if (button != null)
                button.onClick.RemoveListener(OnAddButtonClicked);
        }
    }
}
