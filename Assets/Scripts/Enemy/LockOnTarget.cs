using UnityEngine;

/// <summary>
/// 锁定目标组件 — 两个跟随动画的"点"：
///   锁定点(胸)  ：索敌 UI 圆圈 / 命中取景点，自动找 Chest/Spine 骨骼。
///   预警闪光(头)：怪物攻击前的提示特效点，**只手动拖**（不自动找）。
/// 锁定点自动找骨失败时才用 根位置 + 偏移 兜底。
/// </summary>
public class LockOnTarget : MonoBehaviour
{
    [Header("锁定点(胸)")]
    [Tooltip("偏移量（相对于敌人根位置），自动找骨失败时兜底用")]
    public Vector3 lockOnPointOffset = new Vector3(0, 1f, 0);

    [Tooltip("锁定点：手动拖胸骨（跟随动画）。留空自动找")]
    public Transform lockPoint;

    [Tooltip("锁定点自动找骨关键字（胸部）：优先 Chest/Thorax，其次最深 Spine（上段脊柱≈胸口）")]
    public string[] lockBoneKeywords = { "Chest", "Thorax", "Spine" };

    [Header("相机聚焦点（取景组权重焦点，和锁定点独立）")]
    [Tooltip("相机聚焦点偏移（相对敌人根位置），y 可调；用于算相机权重焦点")]
    public Vector3 cameraFocusOffset = new Vector3(0, 1f, 0);

    [Header("攻击预警闪光(头)")]
    [Tooltip("预警闪光点：手动拖头骨（跟随动画）。不自动找，留空则退回怪物根节点")]
    public Transform telegraphPoint;

    private Transform _autoLockPoint;
    private Transform _cameraFocusPoint;

    void Awake()
    {
        if (lockPoint == null)
        {
            _autoLockPoint = FindDeepestBone(transform, lockBoneKeywords);
            if (_autoLockPoint == null)
                _autoLockPoint = GetOrCreateRootOffsetPoint();   // 没指定骨骼 → 根节点 + 偏移（可调）
        }

        Debug.Log($"[LockOnTarget] {name} 锁定点(胸): {(_autoLockPoint != null ? GetPath(_autoLockPoint) : "未找到→用根+偏移")} | 预警闪光(头): {(telegraphPoint != null ? GetPath(telegraphPoint) : "未手动拖→用根节点")}");
    }

    /// <summary>根节点 + lockOnPointOffset 的跟随点（y 由 Inspector 的 lockOnPointOffset 调）</summary>
    Transform GetOrCreateRootOffsetPoint()
    {
        var existing = transform.Find("LockPointOffset");
        if (existing != null)
        {
            existing.localPosition = lockOnPointOffset;
            return existing;
        }

        var go = new GameObject("LockPointOffset");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = lockOnPointOffset;
        return go.transform;
    }

    void LateUpdate()
    {
        // 运行时微调 lockOnPointOffset 时，偏移点跟着动（方便调手感）
        if (_autoLockPoint != null && _autoLockPoint.name == "LockPointOffset")
            _autoLockPoint.localPosition = lockOnPointOffset;

        // 相机聚焦点跟随 cameraFocusOffset（运行时微调也生效）
        if (_cameraFocusPoint != null)
            _cameraFocusPoint.localPosition = cameraFocusOffset;
    }

    /// <summary>相机聚焦点：根节点 + cameraFocusOffset（用于取景组权重焦点，跟随动画根）</summary>
    public Transform CameraFocusTransform
    {
        get
        {
            if (_cameraFocusPoint == null)
            {
                _cameraFocusPoint = transform.Find("CameraFocusPoint");
                if (_cameraFocusPoint == null)
                {
                    var go = new GameObject("CameraFocusPoint");
                    go.transform.SetParent(transform, false);
                    _cameraFocusPoint = go.transform;
                }
            }
            _cameraFocusPoint.localPosition = cameraFocusOffset;
            return _cameraFocusPoint;
        }
    }

    /// <summary>Inspector 右键 → 重新查找并打印锁定点骨骼路径</summary>
    [ContextMenu("重新查找锁定点骨骼并打印")]
    void DebugFindBone()
    {
        _autoLockPoint = FindDeepestBone(transform, lockBoneKeywords);
        Debug.Log($"[LockOnTarget] {name} 锁定点(胸): {(_autoLockPoint != null ? GetPath(_autoLockPoint) : "未找到，用根+偏移")} | 预警闪光(头): {(telegraphPoint != null ? GetPath(telegraphPoint) : "未手动拖→用根节点")}");
    }

    /// <summary>实际锁定点（lockPoint → 自动胸骨 → 自身兜底，永不 null）</summary>
    public Transform LockPointTransform
    {
        get
        {
            if (lockPoint != null) return lockPoint;
            if (_autoLockPoint != null) return _autoLockPoint;
            return transform;
        }
    }

    /// <summary>实际预警闪光点（telegraphPoint → 自身兜底，只手动拖，不自动找）</summary>
    public Transform TelegraphPointTransform
    {
        get
        {
            if (telegraphPoint != null) return telegraphPoint;
            return transform;
        }
    }

    /// <summary>获取世界空间锁定点坐标（有胸骨则跟随动画）</summary>
    public Vector3 GetLockOnPosition()
    {
        var p = LockPointTransform;
        if (p != transform)
            return p.position;
        return transform.position + lockOnPointOffset;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetLockOnPosition(), 0.15f);
    }

    /// <summary>按关键字顺序找最深匹配的骨骼，找不到返回 null</summary>
    private static Transform FindDeepestBone(Transform root, string[] keywords)
    {
        if (root == null) return null;
        foreach (var kw in keywords)
        {
            if (string.IsNullOrEmpty(kw)) continue;
            var found = FindDeepest(root, kw);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindDeepest(Transform node, string keyword)
    {
        Transform best = null;
        foreach (Transform child in node)
        {
            var deeper = FindDeepest(child, keyword);
            if (deeper != null) best = deeper;   // 深度优先，最后覆盖的是最深匹配
            if (child.name.Contains(keyword)) best = child;
        }
        return best;
    }

    /// <summary>骨骼的完整层级路径（如 怪兽/Bip001/Pelvis/Spine/Spine2），方便定位</summary>
    private static string GetPath(Transform t)
    {
        if (t == null) return "null";
        var names = new System.Collections.Generic.List<string>();
        var cur = t;
        while (cur != null)
        {
            names.Insert(0, cur.name);
            cur = cur.parent;
        }
        return string.Join("/", names);
    }
}
