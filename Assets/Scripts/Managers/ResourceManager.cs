using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 资源管理器 — 异步加载 + 缓存
/// </summary>
public class ResourceManager : GameModule<ResourceManager>
{
    private readonly Dictionary<string, Object> _cache = new();

    protected override void OnInit()
    {
        _cache.Clear();
        Debug.Log("[ResourceManager] 初始化完成");
    }

    public async UniTask<T> LoadAsync<T>(string path) where T : Object
    {
        if (_cache.TryGetValue(path, out var obj))
            return obj as T;

        var request = Resources.LoadAsync<T>(path);
        await request.ToUniTask();

        var result = request.asset as T;
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
}
