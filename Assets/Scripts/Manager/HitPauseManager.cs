using UnityEngine;
using SingletonTool;

/// <summary>
/// 顿帧管理器（单例）— 命中时冻结时间产生"卡肉感"
/// 利用 TimerManager.GetRealTimer（不受 timeScale 影响）恢复时间
/// </summary>
public class HitPauseManager : Singleton<HitPauseManager>
{
    /// <summary>
    /// 触发顿帧
    /// </summary>
    /// <param name="duration">冻结时长（真实秒）</param>
    /// <param name="timeScale">冻结倍率，默认 0.05（几乎暂停）</param>
    public void Trigger(float duration, float timeScale = 0.05f)
    {
        Time.timeScale = timeScale;
        TimerManager.Instance.GetRealTimer(duration, () => Time.timeScale = 1f);
    }
}
