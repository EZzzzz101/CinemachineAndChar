using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

/// <summary>
/// 战斗 UI 加载器 — 常驻模块
/// 进入 Main 战斗场景时自动从资源提供者加载 GamePanel（HUD）并预加载 WinView（胜利面板，隐藏），
/// 离开战斗场景时关闭/隐藏。UI 不再手动摆进场景。
/// LockOnIndicator 是场景预置，不归这里管。
/// </summary>
public class BattleUILoader : GameModule<BattleUILoader>
{
    private GameObject _winView;
    private bool _inBattle;
    private System.Action<object> _onHotUpdateCompleted;

    protected override void Awake()
    {
        base.Awake();
        if (BattleUILoader.Instance != this) return;

        _onHotUpdateCompleted = _ => OnHotUpdateCompleted();
        SceneManager.sceneLoaded += OnSceneLoaded;
        EventBus.Subscribe<object>(GameEvents.HotUpdateCompleted, _onHotUpdateCompleted);
        ApplyForScene(SceneManager.GetActiveScene().name);   // 直接从 Main 启动也生效
    }

    protected override void OnInit()
    {
        Debug.Log("[BattleUILoader] 初始化完成");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyForScene(scene.name);
    }

    private void ApplyForScene(string sceneName)
    {
        if (sceneName == "Main")
            EnterBattleUI();
        else
            ExitBattleUI();
    }

    private void EnterBattleUI()
    {
        if (_inBattle) return;
        _inBattle = true;

        // HUD：GamePanel 是 UIView，走 UIManager 缓存（打开/关闭统一管理）
        UIManager.Instance.Open<GamePanel>();

        // WinView：预加载并隐藏，怪物死亡时 GamePanel 触发 Open&lt;WinView&gt;() 直接显示缓存实例
        PreloadWinView();
    }

    private void ExitBattleUI()
    {
        if (!_inBattle) return;
        _inBattle = false;

        UIManager.Instance.Close<GamePanel>();
        if (_winView != null)
            _winView.SetActive(false);
    }

    private async void PreloadWinView()
    {
        if (_winView != null) return;

        var prefab = await ResourceManager.Instance.LoadAsync<GameObject>("UI/Panels/WinView");
        if (prefab == null)
        {
            Debug.LogWarning("[BattleUILoader] 未找到 WinView 预制体：UI/Panels/WinView");
            return;
        }

        UIManager.Instance.EnsureRoot();
        _winView = Object.Instantiate(prefab, UIManager.Instance.RootTransform);
        _winView.SetActive(false);   // 预加载但不显示
    }

    protected override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EventBus.Unsubscribe<object>(GameEvents.HotUpdateCompleted, _onHotUpdateCompleted);
        base.OnDestroy();
    }

    /// <summary>
    /// 热更完成（provider 切到 AB）后重新拉 HUD：
    /// 直进 Main 场景时热更流程在场景加载后才跑完，首次 Open&lt;GamePanel&gt; 可能因资源未就绪失败，
    /// 完成后再开一次（UIManager 缓存复用，幂等）。
    /// </summary>
    private void OnHotUpdateCompleted()
    {
        if (SceneManager.GetActiveScene().name != "Main") return;
        UIManager.Instance.Open<GamePanel>();
        PreloadWinView();
    }
}
