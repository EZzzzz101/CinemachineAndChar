using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 登录窗口 — 由 UIManager 从 Resources/UI/Panels 实例化的面板。
/// Awake 时注册到 UIManager；克隆出来即显示（不做"先藏再显"）。
/// 读条完成后由 LoadingFlow 调 UIManager.Open&lt;GameLaunchView&gt;() 创建并显示。
/// "进入游戏"按钮点击后切换到下一个场景（暂不做唯一名校验，后续加）。
/// </summary>
public class GameLaunchView : UIView
{
    [Header("进入游戏")]
    [Tooltip("进入游戏按钮；留空自动从子物体查找 Button")]
    [SerializeField] private Button enterButton;

    [Tooltip("点击进入游戏后加载的场景名")]
    [SerializeField] private string nextSceneName = "Main";

    protected override void Awake()
    {
        base.Awake();                   // 注册到 UIManager（_views 缓存）

        if (enterButton == null)
            enterButton = GetComponentInChildren<Button>(true);
        if (enterButton != null)
            enterButton.onClick.AddListener(OnEnterGameClicked);
    }

    private void OnEnterGameClicked()
    {
        // 先关闭登录窗口：它的全屏半透明遮罩在常驻 Canvas 上会跨场景存活，盖暗下一个场景
        gameObject.SetActive(false);
        SceneLoader.Instance.LoadScene(nextSceneName);
    }
}
