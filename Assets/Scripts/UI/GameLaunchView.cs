using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 登录窗口 — 输入玩家名，点"进入游戏"时自动连接大厅并注册（用户名唯一）。
/// 注册成功才切到下一场景；失败显示原因留在登录页。
/// 玩家名输入框在预制体里（InputField (TMP)），代码自动查找。
/// </summary>
public class GameLaunchView : UIView
{
    [Header("进入游戏")]
    [Tooltip("进入游戏按钮；留空自动从子物体查找 Button")]
    [SerializeField] private Button enterButton;

    [Tooltip("点击进入游戏后加载的场景名")]
    [SerializeField] private string nextSceneName = "Main";

    [Header("大厅登录")]
    [Tooltip("玩家名输入框（可空；留空自动从子物体找 TMP_InputField）")]
    [SerializeField] private TMP_InputField nameInput;

    private bool _connecting;

    protected override void Awake()
    {
        base.Awake();                   // 注册到 UIManager（_views 缓存）

        if (enterButton == null)
            enterButton = GetComponentInChildren<Button>(true);
        if (enterButton != null)
            enterButton.onClick.AddListener(OnEnterGameClicked);

        if (nameInput == null)
            nameInput = GetComponentInChildren<TMP_InputField>(true);
    }

    /// <summary>点"进入游戏"：自动连接大厅 → 注册成功 → 进下一场景</summary>
    private async void OnEnterGameClicked()
    {
        if (_connecting) return;

        var name = nameInput != null ? nameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(name))
        {
            Debug.Log("[Login] 请输入玩家名");
            return;
        }

        var service = LobbyClientService.Instance;

        // 已经注册过（如重开登录页）：直接进
        if (service.Registered)
        {
            EnterGame();
            return;
        }

        _connecting = true;
        Debug.Log("[Login] 连接大厅中...");

        // 订阅一次注册结果：成功进游戏，失败显示原因
        Action<string> resultHandler = null;
        resultHandler = reason =>
        {
            service.OnRegisterResult -= resultHandler;
            if (!_connecting) return;
            _connecting = false;
            Debug.Log($"[Login] 注册结果：{reason}");
            if (service.Registered)
                EnterGame();
        };
        service.OnRegisterResult += resultHandler;

        // 订阅一次错误（连接被拒等即时反馈）
        Action<string> errorHandler = null;
        errorHandler = msg =>
        {
            service.OnError -= errorHandler;
            if (!_connecting) return;
            service.OnRegisterResult -= resultHandler;
            _connecting = false;
            Debug.Log("[Login] 错误：" + msg);
        };
        service.OnError += errorHandler;

        service.Connect(name);

        // 超时保护：几秒没回执说明服务器没开/连不上
        await UniTask.Delay(3000);
        if (_connecting)
        {
            service.OnRegisterResult -= resultHandler;
            service.OnError -= errorHandler;
            _connecting = false;
            Debug.Log("[Login] 连接大厅失败：服务器没开？检查地址/端口");
        }
    }

    private void EnterGame()
    {
        // 先关闭登录窗口 + Boot 专属 UI（BG/读条）：都在常驻 Canvas 上，不藏会盖住下一个场景
        gameObject.SetActive(false);
        PersistentUIRoot.Instance?.HideBootUI();
        SceneLoader.Instance.LoadScene(nextSceneName);
    }

}
