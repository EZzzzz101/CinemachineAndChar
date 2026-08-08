using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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

    [Tooltip("点击开始战斗后加载的场景名")]
    [SerializeField] private string nextSceneName = "Main";

    [Header("本地玩家立绘（单机一号位）")]
    [Tooltip("拖入立绘 Sprite；留空时尝试 Resources/UI/Portraits/Player")]
    [SerializeField] private Sprite localPortrait;

    private Image _slot1Portrait;

    protected override void Awake()
    {
        base.Awake();                   // 注册到 UIManager（_views 缓存）

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

        PlayerInputGate.EnterUI();      // 首次实例化即锁输入 + 显鼠标
        ApplyLocalPlayerPortrait();     // 单机进入：一号位刷本地立绘
    }

    public override void Show()
    {
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
    private void ApplyLocalPlayerPortrait()
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

        var sprite = localPortrait != null ? localPortrait : Resources.Load<Sprite>("UI/Portraits/Player");
        if (sprite != null)
            _slot1Portrait.sprite = sprite;
        else
            Debug.LogWarning("[TeamUpView] 未配置本地玩家立绘：拖 localPortrait 或放 Resources/UI/Portraits/Player");
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
        PlayerInputGate.ExitUI();       // 恢复操控 + 隐藏鼠标
        gameObject.SetActive(false);
        SceneLoader.Instance.LoadScene(nextSceneName);
    }

    private void OnDestroy()
    {
        if (FightButton != null)
            FightButton.onClick.RemoveListener(OnFightButtonClicked);
        foreach (var button in AddButtons)
        {
            if (button != null)
                button.onClick.RemoveListener(OnAddButtonClicked);
        }
    }
}
