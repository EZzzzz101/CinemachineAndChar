using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// AssetBundle 资源提供者 — 热更主链路（M13 构建产物 + M14 下载后的本地目录）。
/// 初始化：加载 AssetBundleManifest（bundle 依赖）与 bundlemap.json（地址 → bundle + 资源全路径）。
/// 加载：按 bundlemap 找 bundle（先递归加载依赖），bundle.LoadAssetAsync(资源全路径)。
/// 地址不在映射里 → 回退 Resources（兼容遗留地址）。
/// </summary>
public class AssetBundleAssetProvider : IAssetProvider
{
    [Serializable]
    public class BundlemapEntry
    {
        public string address;   // 运行时地址（小写）：prefabs/安比、ui/panels/gamepanel、main
        public string assetPath; // bundle 内资源全路径（小写）：assets/gameassets/prefabs/安比.prefab
        public string bundle;    // bundle 名：character/player、ui/panels、scene/main
    }

    [Serializable]
    public class BundlemapData
    {
        public List<BundlemapEntry> entries = new();
    }

    private readonly Dictionary<string, BundlemapEntry> _map = new();
    private readonly Dictionary<string, AssetBundle> _bundles = new();
    private readonly ResourcesAssetProvider _fallback = new();

    private AssetBundleManifest _manifest;
    private string _bundleRoot;

    public bool IsReady => _manifest != null;
    public string BundleRoot => _bundleRoot;

    /// <summary>
    /// 初始化：加载 manifest + bundlemap.json；失败返回 false（调用方走兜底）。
    /// manifestBundleName：Unity 约定 manifest bundle 文件 = 构建输出目录名（如 Windows），
    /// 由构建工具写进 filelist.json 的 rootBundle 字段，运行时读出来传进来。
    /// </summary>
    public bool Initialize(string bundleRoot, string manifestBundleName = "AssetBundleManifest")
    {
        Reset();
        _bundleRoot = bundleRoot;

        var manifestBundle = AssetBundle.LoadFromFile(Path.Combine(bundleRoot, manifestBundleName));
        if (manifestBundle == null)
        {
            Debug.LogError($"[AssetBundleAssetProvider] 找不到 manifest bundle：{bundleRoot}/{manifestBundleName}");
            return false;
        }
        _manifest = manifestBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
        if (_manifest == null)
        {
            Debug.LogError("[AssetBundleAssetProvider] manifest 资源加载失败");
            return false;
        }

        var mapPath = Path.Combine(bundleRoot, "bundlemap.json");
        if (File.Exists(mapPath))
        {
            var data = JsonUtility.FromJson<BundlemapData>(File.ReadAllText(mapPath));
            if (data != null)
            {
                foreach (var entry in data.entries)
                    _map[entry.address] = entry;
            }
        }
        else
        {
            Debug.LogWarning($"[AssetBundleAssetProvider] 未找到 bundlemap.json：{mapPath}");
        }

        Debug.Log($"[AssetBundleAssetProvider] 初始化完成，{_map.Count} 条地址映射");
        return true;
    }

    public async UniTask<T> LoadAsync<T>(string address) where T : UnityEngine.Object
    {
        if (!_map.TryGetValue(address.ToLowerInvariant(), out var entry))
        {
            Debug.LogWarning($"[AssetBundleAssetProvider] 地址 {address} 不在 AB 映射里，回退 Resources");
            return await _fallback.LoadAsync<T>(address);
        }

        var bundle = await LoadBundleAsync(entry.bundle);
        if (bundle == null) return null;

        var handle = bundle.LoadAssetAsync<T>(entry.assetPath);
        await handle.ToUniTask();
        if (handle.asset == null)
            Debug.LogWarning($"[AssetBundleAssetProvider] {entry.assetPath} 加载为空（类型 {typeof(T).Name}）");
        return handle.asset as T;
    }

    public async UniTask LoadSceneAsync(string sceneName, IProgress<float> progress = null)
    {
        if (!_map.TryGetValue(sceneName.ToLowerInvariant(), out var entry))
        {
            Debug.LogWarning($"[AssetBundleAssetProvider] 场景 {sceneName} 不在 AB 映射里，走 Build Settings 场景名");
            var fallbackOp = SceneManager.LoadSceneAsync(sceneName);
            while (fallbackOp != null && !fallbackOp.isDone)
            {
                progress?.Report(fallbackOp.progress);
                await UniTask.Yield();
            }
            return;
        }

        var bundle = await LoadBundleAsync(entry.bundle);
        if (bundle == null) return;

        var op = SceneManager.LoadSceneAsync(entry.assetPath);
        if (op == null)
        {
            Debug.LogError($"[AssetBundleAssetProvider] AB 场景加载失败：{entry.assetPath}");
            return;
        }
        while (!op.isDone)
        {
            progress?.Report(op.progress);
            await UniTask.Yield();
        }
    }

    /// <summary>加载 bundle（先递归加载依赖，再缓存）</summary>
    private async UniTask<AssetBundle> LoadBundleAsync(string bundleName)
    {
        if (_bundles.TryGetValue(bundleName, out var cached) && cached != null)
            return cached;

        if (_manifest != null)
        {
            foreach (var dep in _manifest.GetAllDependencies(bundleName))
                await LoadBundleAsync(dep);
        }

        var request = AssetBundle.LoadFromFileAsync(Path.Combine(_bundleRoot, bundleName));
        await request.ToUniTask();
        if (request.assetBundle == null)
        {
            Debug.LogError($"[AssetBundleAssetProvider] bundle 加载失败：{bundleName}");
            return null;
        }

        _bundles[bundleName] = request.assetBundle;
        return request.assetBundle;
    }

    public void Reset()
    {
        foreach (var bundle in _bundles.Values)
        {
            if (bundle != null) bundle.Unload(false);
        }
        _bundles.Clear();
        _map.Clear();
        _manifest = null;
        _bundleRoot = null;
    }
}
