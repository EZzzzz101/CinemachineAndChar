#if UNITY_EDITOR
using System;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 编辑器开发兜底（对应 YooAsset 方案的 EditorSimulateMode）—
/// 未构建/未启用 AB 时，直接按地址规则从 Assets/GameAssets 用 AssetDatabase 加载。
/// 仅编辑器内可用；正式包必须走 AB。
/// </summary>
public class EditorAssetProvider : IAssetProvider
{
    /// <summary>常见扩展名（按优先级），预加载用 Object 泛型时无法按类型推断扩展名，逐个尝试</summary>
    private static readonly string[] CandidateExtensions =
    {
        ".prefab", ".mp3", ".png", ".mat", ".anim", ".json", ".asset", ".txt",
    };

    public UniTask<T> LoadAsync<T>(string address) where T : UnityEngine.Object
    {
        foreach (var ext in CandidateExtensions)
        {
            var fullPath = $"Assets/GameAssets/{address}{ext}";
            var asset = AssetDatabase.LoadAssetAtPath<T>(fullPath);
            if (asset != null)
                return UniTask.FromResult(asset);
        }

        Debug.LogWarning($"[EditorAssetProvider] 未找到 Assets/GameAssets/{address}（已按常见扩展名尝试）");
        return UniTask.FromResult<T>(null);
    }

    public async UniTask LoadSceneAsync(string sceneName, IProgress<float> progress = null)
    {
        var op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"[EditorAssetProvider] 场景 {sceneName} 不存在（检查 Build Settings）");
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
#endif
