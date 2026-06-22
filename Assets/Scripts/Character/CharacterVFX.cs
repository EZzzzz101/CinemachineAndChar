using UnityEngine;
using System;

/// <summary>
/// 角色特效管理（单例）— Animation Event 接收端
/// 挂在角色 GameObject 上，Instantiate + 自销毁（后续换对象池接口不变）
/// </summary>
public class CharacterVFX : MonoBehaviour
{
    public static CharacterVFX Instance { get; private set; }

    [Serializable]
    public struct VFXEntry
    {
        public string name;
        public GameObject prefab;
        public Vector3 rotationOffset;
        public Vector3 positionOffset;
    }

    [Header("特效映射表")]
    [SerializeField] private VFXEntry[] _vfxEntries;

    [Header("生成位置")]
    [SerializeField] private Transform _vfxSpawnPoint;    // 武器骨骼等，空则用自身

    private Transform SpawnPoint => _vfxSpawnPoint != null ? _vfxSpawnPoint : transform;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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
        var pos = SpawnPoint.position + SpawnPoint.rotation * entry.Value.positionOffset;
        var rot = SpawnPoint.rotation * Quaternion.Euler(entry.Value.rotationOffset);
        SpawnAndAutoDestroy(entry.Value.prefab, pos, rot);
    }

    /// <summary>
    /// Combo 每段攻击特效 — ComboNext() 代码直调
    /// </summary>
    public void PlayComboVFX(GameObject prefab)
    {
        SpawnAndAutoDestroy(prefab, SpawnPoint.position, SpawnPoint.rotation);
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

    private void SpawnAndAutoDestroy(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, pos, rot, SpawnPoint);
        Destroy(go, 2f);
    }
}
