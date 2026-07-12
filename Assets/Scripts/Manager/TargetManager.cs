using System.Collections.Generic;
using UnityEngine;
using SingletonTool;

/// <summary>
/// 目标管理器 — 维护场景中所有 Targetable 的注册表
/// 提供按阵营/距离的查询方法
/// </summary>
public class TargetManager : Singleton<TargetManager>
{
    private readonly List<Targetable> _allTargets = new();

    /// <summary>只读访问 — 外部只能遍历，不能 Add/Remove</summary>
    public IReadOnlyList<Targetable> AllTargets => _allTargets;

    #region 注册/注销
    public void Register(Targetable target)
    {
        if (!_allTargets.Contains(target))
            _allTargets.Add(target);
    }

    public void Unregister(Targetable target)
    {
        _allTargets.Remove(target);
    }
    #endregion

    #region 查询
    /// <summary>按阵营过滤</summary>
    public List<Targetable> GetByTeam(Team team)
    {
        var result = new List<Targetable>();
        foreach (var t in _allTargets)
        {
            if (t != null && t.Team == team)
                result.Add(t);
        }
        return result;
    }

    /// <summary>在范围内找最近的指定阵营目标，排除自身</summary>
    public Targetable FindNearest(Vector3 from, float maxRange, 
        Team targetTeam, GameObject excludeSelf = null)
    {
        Targetable nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var t in _allTargets)
        {
            if (t == null) continue;
            if (t.Team != targetTeam) continue;
            if (excludeSelf != null && t.gameObject == excludeSelf) continue;

            float dist = Vector3.Distance(from, t.transform.position);
            if (dist <= maxRange && dist < nearestDist)
            {
                nearestDist = dist;
                nearest = t;
            }
        }
        return nearest;
    }
    #endregion
}
