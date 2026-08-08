using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 资源提供者接口 — ResourceManager 通过它加载资源，屏蔽底层来源。
///
/// 实现：
///   AssetBundleAssetProvider —— 热更主链路（AB 构建后走这里）；
///   EditorAssetProvider      —— 编辑器开发兜底（未构建 AB 时直接从 Assets/GameAssets 按路径加载）；
///   ResourcesAssetProvider   —— Resources 兜底（保留，正式包未构建 AB 时兜底）。
///
/// 地址约定（与旧 Resources 路径一致，不含扩展名）：
///   "Prefabs/安比" / "UI/Panels/GamePanel" / "BGM/World" / "UI/Portraits/Player"
/// 场景单独走 LoadSceneAsync(sceneName)。
/// </summary>
public interface IAssetProvider
{
    /// <summary>按资源地址异步加载资源</summary>
    UniTask<T> LoadAsync<T>(string address) where T : UnityEngine.Object;

    /// <summary>异步切换场景（AB 模式从 bundle 加载，兜底模式走 Build Settings 场景名）</summary>
    UniTask LoadSceneAsync(string sceneName, IProgress<float> progress = null);

    /// <summary>清理资源（卸载 bundle 等），切换提供者时调用</summary>
    void Reset();
}
