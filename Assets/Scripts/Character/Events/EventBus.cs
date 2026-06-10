using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局事件总线
/// 只有两个核心方法：Emit和 Subscribe
/// 单例模式
/// </summary>
public class EventBus
{
    //事件名字->回调函数列表
    private readonly Dictionary<string,List<Action<object>>> _listeners = new();
    //单例模式
    private static EventBus _instance;
    public static EventBus Instance =>_instance??=new EventBus();

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <param name="eventname">事件名称</param>
    /// <param name="callback">回调函数名称</param>
    private void Subscribe(string eventname,Action<object> callback)
    {
        //没有回调列表就创建
        if (!_listeners.ContainsKey(eventname))
            _listeners[eventname]=new List<Action<object>>();
        _listeners[eventname].Add(callback);
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    public void Unsubscribe(string eventName, Action<object> callback)
    {
        if (_listeners.ContainsKey(eventName))
            _listeners[eventName].Remove(callback);
    }

    /// <summary>发布事件：通知所有订阅者</summary>
    public void Emit(string eventName, object data = null)
    {
        if (!_listeners.ContainsKey(eventName)) return;

        // 复制一份再遍历，防止回调里修改列表导致报错
        var callbacks = new List<Action<object>>(_listeners[eventName]);
        foreach (var cb in callbacks)
            cb?.Invoke(data);
    }


}
