using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景加载器 — 退出当前 → 显示Loading → 异步加载 → 进入新场景
/// </summary>
public class SceneLoader : GameModule<SceneLoader>
{
    [SerializeField] private GameObject _loadingUI;

    protected override void OnInit()
    {
        Debug.Log("[SceneLoader] 初始化完成");
    }

    /// <summary>切换到目标场景</summary>
    public async void LoadScene(string sceneName)
    {
        if (_loadingUI != null)
            _loadingUI.SetActive(true);

        await SceneManager.LoadSceneAsync(sceneName).ToUniTask();

        if (_loadingUI != null)
            _loadingUI.SetActive(false);
    }
}
