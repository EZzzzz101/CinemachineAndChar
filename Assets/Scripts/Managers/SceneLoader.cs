using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景加载器 — 退出当前场景 → 按目标场景显示对应加载面板 → 异步加载 → 隐藏面板
/// 加载面板按场景名从资源提供者（AB/编辑器兜底）读取并缓存实例，挂到常驻 UI 根下（跨场景存活）。
/// 场景本身也走 provider：AB 模式从 bundle 加载场景，兜底模式走 Build Settings 场景名。
/// </summary>
public class SceneLoader : GameModule<SceneLoader>
{
    [Header("场景加载面板（Assets/GameAssets/UI/Panels 下的预制体名，留空则不显示）")]
    [Tooltip("六分街（非战斗场景）加载面板")]
    [SerializeField] private string loadingPanelSixthStreet = "LoadingPanelSixthStreet";
    [Tooltip("Main（战斗场景）加载面板")]
    [SerializeField] private string loadingPanelMain = "LoadingPanelMain";

    private readonly Dictionary<string, GameObject> _panelCache = new();

    protected override void OnInit()
    {
        Debug.Log("[SceneLoader] 初始化完成");
    }

    /// <summary>切换到目标场景，期间显示对应加载面板</summary>
    public async void LoadScene(string sceneName)
    {
        var panel = await GetOrCreatePanelAsync(GetPanelName(sceneName));
        if (panel != null)
        {
            panel.transform.SetAsLastSibling();   // 保证盖在其他 UI 上面
            panel.SetActive(true);
        }

        var flow = panel != null ? panel.GetComponentInChildren<LoadingFlow>() : null;
        // 场景加载进度最高 clamp 到 0.9：LoadingFlow 满 1 会开登录窗（Boot 专用流程），不能误触
        var progress = flow != null
            ? new System.Progress<float>(p => flow.SetProgress(Mathf.Min(p, 0.9f)))
            : null;

        await ResourceManager.Instance.Provider.LoadSceneAsync(sceneName, progress);

        if (panel != null)
            panel.SetActive(false);
    }

    /// <summary>目标场景 → 加载面板预制体名（两个场景字段，后续加场景再扩）</summary>
    private string GetPanelName(string sceneName)
    {
        return sceneName switch
        {
            "Main" => loadingPanelMain,
            _ => loadingPanelSixthStreet,   // 六分街及其他非战斗场景
        };
    }

    private async UniTask<GameObject> GetOrCreatePanelAsync(string panelName)
    {
        if (string.IsNullOrEmpty(panelName)) return null;

        if (_panelCache.TryGetValue(panelName, out var cached) && cached != null)
            return cached;

        var prefab = await ResourceManager.Instance.LoadAsync<GameObject>($"UI/Panels/{panelName}");
        if (prefab == null)
        {
            Debug.LogWarning($"[SceneLoader] 加载面板不存在：UI/Panels/{panelName}，场景照样加载");
            return null;
        }

        UIManager.Instance.EnsureRoot();
        var go = Object.Instantiate(prefab, UIManager.Instance.RootTransform);
        go.SetActive(false);
        _panelCache[panelName] = go;
        return go;
    }
}
