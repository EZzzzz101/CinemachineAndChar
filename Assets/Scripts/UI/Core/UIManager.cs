using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI 总控 — View 用类型做 key 缓存，懒加载
/// View 继承 UIView 自动注册，不需要手动调 Register
/// 
/// </summary>
public class UIManager : GameModule<UIManager>
{
    [Header("场景引用")]
    [SerializeField] private Transform _root;
    [SerializeField] private Canvas _canvas;

    private readonly Dictionary<Type, IUIView> _views = new();

    public Canvas RootCanvas => _canvas;
    public Transform RootTransform => _root;

    protected override void OnInit()
    {
        Debug.Log("[UIManager] 初始化完成");
    }

    public void Register(IUIView view)
    {
        _views[view.GetType()] = view;
    }

    public T Open<T>() where T : IUIView
    {
        var type = typeof(T);

        if (_views.TryGetValue(type, out var view))
        {
            view.Show();
            return (T)view;
        }

        var prefab = Resources.Load<GameObject>($"UI/Panels/{type.Name}");
        if (prefab != null)
        {
            var go = Instantiate(prefab, _root);
            go.SetActive(true);
            return go.GetComponent<T>();
        }

        return default;
    }

    public void Close<T>() where T : IUIView
    {
        if (_views.TryGetValue(typeof(T), out var view))
            view.Hide();
    }

    public T Get<T>() where T : IUIView
    {
        if (_views.TryGetValue(typeof(T), out var view))
            return (T)view;
        return default;
    }
}
