using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用对象池 — 用于伤害数字等频繁创建销毁的对象
/// </summary>
public class UIObjectPool<T> where T : Component
{
    private readonly T _prefab;
    private readonly Transform _parent;
    private readonly Queue<T> _pool = new();
    private readonly int _maxSize;

    public UIObjectPool(T prefab, Transform parent, int preAlloc = 10, int maxSize = 50)
    {
        _prefab = prefab;
        _parent = parent;
        _maxSize = maxSize;

        for (int i = 0; i < preAlloc; i++)
        {
            T obj = Object.Instantiate(_prefab, _parent);
            obj.gameObject.SetActive(false);
            _pool.Enqueue(obj);
        }
    }

    public T Get()
    {
        if (_pool.Count > 0)
        {
            T obj = _pool.Dequeue();
            obj.gameObject.SetActive(true);
            return obj;
        }
        return Object.Instantiate(_prefab, _parent);
    }

    public void Return(T obj)
    {
        obj.gameObject.SetActive(false);
        if (_pool.Count < _maxSize)
            _pool.Enqueue(obj);
        else
            Object.Destroy(obj.gameObject);
    }
}
