using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resources 兜底实现 — 保留旧 Resources.Load 逻辑。
/// 托管资源已迁移到 Assets/GameAssets（打 AB），这里只兜底仍放在 Resources 下的资产。
/// </summary>
public class ResourcesAssetProvider : IAssetProvider
{
    public async UniTask<T> LoadAsync<T>(string address) where T : UnityEngine.Object
    {
        var request = Resources.LoadAsync<T>(address);
        await request.ToUniTask();
        return request.asset as T;
    }

    public async UniTask LoadSceneAsync(string sceneName, IProgress<float> progress = null)
    {
        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"[ResourcesAssetProvider] 场景 {sceneName} 不存在（检查 Build Settings）");
            return;
        }

        while (!op.isDone)
        {
            progress?.Report(op.progress);
            await UniTask.Yield();
        }
    }

    public void Reset() { }
}
