using System;
using System.Collections.Generic;

/// <summary>
/// 全局事件总线 — 解耦模块通信
/// 用法：
///   EventBus.Subscribe<int>(GameEvents.PlayerHPChanged, OnHPChanged);
///   EventBus.Emit(GameEvents.PlayerHPChanged, 75);
/// </summary>
public static class EventBus
{
    private static readonly Dictionary<string, List<Delegate>> _listeners = new();

    // ===== 泛型版 =====

    public static void Subscribe<T>(string eventName, Action<T> callback)
    {
        if (!_listeners.ContainsKey(eventName))
            _listeners[eventName] = new List<Delegate>();
        _listeners[eventName].Add(callback);
    }

    public static void Unsubscribe<T>(string eventName, Action<T> callback)
    {
        if (_listeners.TryGetValue(eventName, out var list))
        {
            list.Remove(callback);
            if (list.Count == 0)
                _listeners.Remove(eventName);
        }
    }

    public static void Emit<T>(string eventName, T data)
    {
        if (!_listeners.TryGetValue(eventName, out var list)) return;

        // 复制一份防止遍历时修改
        var copy = new List<Delegate>(list);
        foreach (var cb in copy)
        {
            (cb as Action<T>)?.Invoke(data);
        }
    }

    // ===== 无参数版 =====

    public static void Subscribe(string eventName, Action callback)
    {
        Subscribe<object>(eventName, _ => callback());
    }

    public static void Emit(string eventName)
    {
        Emit<object>(eventName, null);
    }

    /// <summary>清空所有监（场景切换时调用）</summary>
    public static void Clear()
    {
        _listeners.Clear();
    }
}
