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

    protected override void Awake()
    {
        base.Awake();
        if (BattleUILoader.Instance != this) return;

        SceneManager.sceneLoaded += OnSceneLoaded;
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
        base.OnDestroy();
    }
}
