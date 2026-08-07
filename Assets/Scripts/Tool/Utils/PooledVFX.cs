using UnityEngine;

/// <summary>
/// VFX 池内实例标记 — 记录所属 prefab、控制自动回收。
/// lifetime<=0 不自动回收（由调用方显式 VFXPool.Despawn，如攻击预警）。
/// 用真实时间（unscaledDeltaTime）回收，避免时停(timeScale<1)期间池被占满。
/// </summary>
public class PooledVFX : MonoBehaviour
{
    public GameObject Prefab { get; private set; }
    public bool InPool { get; private set; }

    private float _lifetime;
    private float _elapsed;

    public void Bind(GameObject prefab)
    {
        Prefab = prefab;
    }

    public void OnSpawned(float lifetime)
    {
        _lifetime = lifetime;
        _elapsed = 0f;
        InPool = false;
    }

    public void OnDespawned()
    {
        _lifetime = 0f;
        InPool = true;
    }

    void Update()
    {
        if (_lifetime <= 0f) return;

        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed >= _lifetime)
            VFXPool.Despawn(gameObject);
    }
}
