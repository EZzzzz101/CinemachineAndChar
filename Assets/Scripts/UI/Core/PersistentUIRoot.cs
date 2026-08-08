using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 常驻 UI 根注册 — 挂在启动场景的 Canvas 上
/// Awake 时把所在 Canvas 注册给 UIManager，之后所有面板都挂到这里，跨场景存活。
/// 注意：所在 Canvas 必须位于 DontDestroyOnLoad 根（如 GameEntry）下，否则切场景会被销毁。
/// 同时负责隐藏启动场景专属 UI（BG、读条等）：离开 Boot 自动藏，回到 Boot 自动显示。
/// </summary>
public class PersistentUIRoot : MonoBehaviour
{
    [Header("Boot 专属 UI（离开启动场景后隐藏）")]
    [Tooltip("留空时自动按名字查找 Canvas 直属子物体：BG / LoadingBar")]
    [SerializeField] private GameObject[] bootOnlyUI;

    private static PersistentUIRoot _instance;
    public static PersistentUIRoot Instance => _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // 注册常驻 Canvas
        var uiCanvas = transform as RectTransform;
        if (uiCanvas != null)
            UIManager.Instance.BindRoot(uiCanvas);

        // 没手动拖引用时，按名字兜底（BG、LoadingBar 都是 Canvas 直属子物体）
        if (bootOnlyUI == null || bootOnlyUI.Length == 0)
        {
            bootOnlyUI = new GameObject[]
            {
                transform.Find("BG")?.gameObject,
                transform.Find("LoadingBar")?.gameObject,
            };
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 进 Boot 显示启动 UI；进其他场景隐藏（兜底，正常流程点击进入游戏时已提前隐藏）
        if (scene.name == "Boot")
            ShowBootUI();
        else
            HideBootUI();
    }

    /// <summary>隐藏启动场景专属 UI（BG、读条等），进游戏前调用</summary>
    public void HideBootUI()
    {
        SetBootUI(false);
    }

    /// <summary>恢复启动场景专属 UI（回 Boot 场景时）</summary>
    public void ShowBootUI()
    {
        SetBootUI(true);
    }

    private void SetBootUI(bool active)
    {
        if (bootOnlyUI == null) return;
        foreach (var go in bootOnlyUI)
        {
            if (go != null)
                go.SetActive(active);
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
