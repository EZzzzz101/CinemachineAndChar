using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景加载器 — 退出当前场景 → 按目标场景显示对应加载面板 → 异步加载 → 隐藏面板
/// 加载面板按场景名从 Resources/UI/Panels 读取并缓存实例，挂到常驻 UI 根下（跨场景存活）。
/// </summary>
public class SceneLoader : GameModule<SceneLoader>
{
    [Header("场景加载面板（Resources/UI/Panels 下的预制体名，留空则不显示）")]
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
        var panel = GetOrCreatePanel(GetPanelName(sceneName));
        if (panel != null)
        {
            panel.transform.SetAsLastSibling();   // 保证盖在其他 UI 上面
            panel.SetActive(true);
        }

        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"[SceneLoader] 场景 {sceneName} 不存在，请检查 Build Settings");
            if (panel != null) panel.SetActive(false);
            return;
        }

        var flow = panel != null ? panel.GetComponentInChildren<LoadingFlow>() : null;
        while (!op.isDone)
        {
            // 单场景模式 progress 最高 0.9，不会误触 LoadingFlow.Complete（那是 Boot 专用流程）
            flow?.SetProgress(op.progress);
            await UniTask.Yield();
        }

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

    private GameObject GetOrCreatePanel(string panelName)
    {
        if (string.IsNullOrEmpty(panelName)) return null;

        if (_panelCache.TryGetValue(panelName, out var cached) && cached != null)
            return cached;

        var prefab = Resources.Load<GameObject>($"UI/Panels/{panelName}");
        if (prefab == null)
        {
            Debug.LogWarning($"[SceneLoader] 加载面板不存在：Resources/UI/Panels/{panelName}，场景照样加载");
            return null;
        }

        UIManager.Instance.EnsureRoot();
        var go = Object.Instantiate(prefab, UIManager.Instance.RootTransform);
        go.SetActive(false);
        _panelCache[panelName] = go;
        return go;
    }
}
