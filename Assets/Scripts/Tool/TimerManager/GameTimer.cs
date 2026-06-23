using System;
using UnityEngine;

/// <summary>
/// 单个计时器 — 不受 timeScale 影响（真实时间）
/// </summary>
public class GameTimer
{
    private float _remaining;
    private Action _callback;
    private bool _running;

    public bool IsRunning => _running;

    /// <summary>启动计时</summary>
    public void Start(float duration, Action callback)
    {
        _remaining = duration;
        _callback = callback;
        _running = true;
    }

    /// <summary>每帧调</summary>
    public void Tick()
    {
        if (!_running) return;
        _remaining -= Time.unscaledDeltaTime;
        if (_remaining <= 0f)
        {
            _callback?.Invoke();
            _running = false;
        }
    }

    /// <summary>提前取消</summary>
    public void Cancel()
    {
        _running = false;
        _callback = null;
        _remaining = 0f;
    }
}
