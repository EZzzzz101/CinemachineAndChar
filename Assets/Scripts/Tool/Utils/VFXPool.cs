using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用 VFX 对象池 — 战斗中高频生成的特效（受击/攻击/预警）统一走这里，避免频繁 Instantiate/Destroy。
/// 按 prefab 分池；Spawn 时自动挂 PooledVFX，lifetime>0 自动回收，<=0 由调用方显式 Despawn（如预警特效）。
/// </summary>
public static class VFXPool
{
    private static readonly Dictionary<GameObject, Queue<GameObject>> _pools = new();

    /// <summary>
    /// 从对象池取一个特效（池空则实例化）。
    /// </summary>
    /// <param name="prefab">特效预制体</param>
    /// <param name="position">世界坐标</param>
    /// <param name="rotation">世界旋转</param>
    /// <param name="parent">父节点（预警特效挂在骨骼上）</param>
    /// <param name="lifetime">自动回收秒数（真实秒）；<=0 由调用方显式 Despawn</param>
    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation,
        Transform parent = null, float lifetime = 2f)
    {
        if (prefab == null) return null;

        if (!_pools.TryGetValue(prefab, out var pool))
        {
            pool = new Queue<GameObject>();
            _pools[prefab] = pool;
        }

        // 从池里拿，跳过被外部销毁的脏实例
        GameObject instance = null;
        while (pool.Count > 0)
        {
            var candidate = pool.Dequeue();
            if (candidate != null)
            {
                instance = candidate;
                break;
            }
        }

        if (instance == null)
        {
            instance = Object.Instantiate(prefab, position, rotation, parent);
            instance.AddComponent<PooledVFX>().Bind(prefab);
        }
        else
        {
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
        }

        var pooled = instance.GetComponent<PooledVFX>();
        if (pooled != null) pooled.OnSpawned(lifetime);
        return instance;
    }

    /// <summary>显式回收（预警特效用）：停用并放回池，供下次复用</summary>
    public static void Despawn(GameObject instance)
    {
        if (instance == null) return;

        var pooled = instance.GetComponent<PooledVFX>();
        if (pooled == null || pooled.Prefab == null)
        {
            Object.Destroy(instance);   // 不是池对象，兜底销毁
            return;
        }
        if (pooled.InPool) return;      // 防重复回收

        pooled.OnDespawned();
        instance.SetActive(false);

        if (!_pools.TryGetValue(pooled.Prefab, out var pool))
        {
            pool = new Queue<GameObject>();
            _pools[pooled.Prefab] = pool;
        }
        pool.Enqueue(instance);
    }

    /// <summary>预热：提前实例化 count 个放进池，避免战斗首帧卡顿</summary>
    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;

        if (!_pools.TryGetValue(prefab, out var pool))
        {
            pool = new Queue<GameObject>();
            _pools[prefab] = pool;
        }

        for (int i = 0; i < count; i++)
        {
            var go = Object.Instantiate(prefab);
            go.AddComponent<PooledVFX>().Bind(prefab);
            go.SetActive(false);
            pool.Enqueue(go);
        }
    }

    /// <summary>清空所有池（场景切换时可调用，释放残留实例）</summary>
    public static void Clear()
    {
        foreach (var pool in _pools.Values)
        {
            while (pool.Count > 0)
            {
                var go = pool.Dequeue();
                if (go != null) Object.Destroy(go);
            }
        }
        _pools.Clear();
    }
}
