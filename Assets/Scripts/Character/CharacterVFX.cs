using UnityEngine;
using System;

/// <summary>
/// 角色特效管理（单例）— Animation Event 接收端
/// 挂在角色 GameObject 上，特效统一走 VFXPool 对象池（自动回收复用）
/// </summary>
public class CharacterVFX : MonoBehaviour
{
    public static CharacterVFX Instance { get; private set; }

    [Serializable]
    public struct VFXEntry
    {
        public string name;
        public GameObject prefab;
        public Transform spawnPoint;          // 该特效的父节点
        public Vector3 rotationOffset;
        public Vector3 positionOffset;
    }

    [Header("特效映射表")]
    [SerializeField] private VFXEntry[] _vfxEntries;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 预热攻击特效，避免战斗中首帧 Instantiate 卡顿
        if (_vfxEntries != null)
        {
            foreach (var entry in _vfxEntries)
            {
                if (entry.prefab != null)
                    VFXPool.Prewarm(entry.prefab, 2);
            }
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ===== 动画事件接收方法 =====

    /// <summary>
    /// 动画关键帧调用：PlayVFX("Slash_1")
    /// </summary>
    public void PlayVFX(string name)
    {
        var entry = GetEntry(name);
        if (entry?.prefab == null) return;

        var sp = entry.Value.spawnPoint != null ? entry.Value.spawnPoint : transform;
        var pos = sp.position + sp.rotation * entry.Value.positionOffset;
        var rot = sp.rotation * Quaternion.Euler(entry.Value.rotationOffset);
        SpawnAndAutoDestroy(entry.Value.prefab, pos, rot, sp);
    }

    /// <summary>
    /// Combo 每段攻击特效 — ComboNext() 代码直调
    /// </summary>
    public void PlayComboVFX(GameObject prefab)
    {
        SpawnAndAutoDestroy(prefab, transform.position, transform.rotation, transform);
    }

    // ===== 内部 =====

    private VFXEntry? GetEntry(string name)
    {
        foreach (var entry in _vfxEntries)
        {
            if (entry.name == name)
                return entry;
        }
        Debug.LogWarning($"CharacterVFX: 未找到特效 '{name}'");
        return null;
    }

    private void SpawnAndAutoDestroy(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent)
    {
        if (prefab == null) return;
        VFXPool.Spawn(prefab, pos, rot, parent, 2f);
    }
}
