using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 胜利结算面板 — 怪物死亡后由 GamePanel 订阅 GameEvents.EnemyDied 打开。
/// 打开时与组队界面一致：锁定角色操控（含镜头转向）、鼠标交给 Canvas；
/// 点"返回六分街"→ 预留联机断开接口 → 加载六分街。
/// </summary>
public class WinView : UIView
{
    [Header("返回按钮（留空自动按文字查找）")]
    [SerializeField] private Button backButton;

    protected override void Awake()
    {
        base.Awake();   // 注册到 UIManager

        AutoFindBackButton();
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
        else
            Debug.LogWarning("[WinView] 未找到返回按钮，请检查预制体");

        PlayerInputGate.EnterUI();   // 锁操控 + 显鼠标
    }

    public override void Show()
    {
        base.Show();
        PlayerInputGate.EnterUI();   // 缓存复用再次打开时同样锁输入
    }

    private void AutoFindBackButton()
    {
        if (backButton != null) return;

        foreach (var b in GetComponentsInChildren<Button>(true))
        {
            var label = b.GetComponentInChildren<TMP_Text>(true);
            if (label != null &&
                (label.text.Contains("返回") || label.text.Contains("六分街") || label.text.Contains("确定")))
            {
                backButton = b;
                break;
            }
        }
    }

    private void OnBackClicked()
    {
        DisconnectMultiplayer();     // 联机预留：先断开再走

        PlayerInputGate.ExitUI();    // 恢复操控 + 隐藏鼠标
        gameObject.SetActive(false);
        SceneLoader.Instance.LoadScene("SixthStreet");
    }

    /// <summary>联机预留接口：切断与其他玩家的连接/通知房间（本地阶段空实现）</summary>
    protected virtual void DisconnectMultiplayer()
    {
        // TODO 联机：M7+ 通知其他玩家"主机已离开"，关闭会话连接
        Debug.Log("[WinView] 预留联机接口：断开与其他玩家的连接");
    }

    private void OnDestroy()
    {
        if (backButton != null)
            backButton.onClick.RemoveListener(OnBackClicked);
    }
}
