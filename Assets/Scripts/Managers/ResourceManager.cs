using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 资源管理器 — 通过 IAssetProvider 加载资源 + 缓存。
/// provider 默认：编辑器用 EditorAssetProvider（Assets/GameAssets 直读，开发不断流），
/// 正式包用 Resources 兜底；热更流程（HotUpdateManager）完成后切换为 AssetBundleAssetProvider（AB 主用）。
/// </summary>
public class ResourceManager : GameModule<ResourceManager>
{
    private readonly Dictionary<string, Object> _cache = new();
    private IAssetProvider _provider;

    /// <summary>当前资源提供者（首次访问时按环境创建默认实现）</summary>
    public IAssetProvider Provider => _provider ??= CreateDefaultProvider();

    protected override void OnInit()
    {
        _cache.Clear();
        _provider = null;   // 下次访问时按当前环境重建默认 provider
        Debug.Log("[ResourceManager] 初始化完成");
    }

    /// <summary>切换资源来源（热更完成后换成 AB）；清缓存防止串渠道</summary>
    public void SetProvider(IAssetProvider provider)
    {
        _provider = provider;
        _cache.Clear();
        Debug.Log($"[ResourceManager] 资源提供者切换为 {provider?.GetType().Name}");
    }

    private static IAssetProvider CreateDefaultProvider()
    {
#if UNITY_EDITOR
        return new EditorAssetProvider();   // 编辑器开发兜底：未构建 AB 也能直读 Assets/GameAssets
#else
        return new ResourcesAssetProvider(); // 正式包必须走 AB；Resources 只兜底残留资源
#endif
    }

    public async UniTask<T> LoadAsync<T>(string path) where T : Object
    {
        if (_cache.TryGetValue(path, out var obj))
            return obj as T;

        var result = await Provider.LoadAsync<T>(path);
        if (result != null)
            _cache[path] = result;
        return result;
    }

    public async UniTask<T> InstantiateAsync<T>(string path, Vector3 pos, Quaternion rot, Transform parent = null) where T : Object
    {
        var prefab = await LoadAsync<GameObject>(path);
        if (prefab == null) return null;
        return Object.Instantiate(prefab, pos, rot, parent) as T;
    }

    /// <summary>绕过缓存直接从当前 provider 加载（热更完成后重新拉取同地址资源用）</summary>
    public UniTask<T> LoadFreshAsync<T>(string path) where T : Object
    {
        return Provider.LoadAsync<T>(path);
    }
}
