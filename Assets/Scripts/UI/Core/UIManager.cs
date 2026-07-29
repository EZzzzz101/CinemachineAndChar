using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI 总控 — 面板用类型做 key 缓存，懒加载
/// 面板继承 UIView 自动注册，不需要手动调 Register
/// </summary>
public class UIManager : GameModule<UIManager>
{
    private readonly Dictionary<Type, IUIView> _panels = new();
    private Transform _root;
    private Canvas _canvas;

    /// <summary>UI Canvas 根节点</summary>
    public Canvas RootCanvas => _canvas;
    /// <summary>UI 根级 Transform（面板挂在这个下面）</summary>
    public Transform RootTransform => _root;

    protected override void OnInit()
    {
        // 子物体：UI Root — 挂 Canvas 组件
        var rootGO = new GameObject("UICanvas");
        rootGO.transform.SetParent(transform, false);

        _canvas = rootGO.AddComponent<Canvas>();
        rootGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        rootGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        _root = rootGO.transform;

        Debug.Log("[UIManager] 初始化完成");
    }

    public void Register(IUIView panel)
    {
        _panels[panel.GetType()] = panel;
    }

    public T Open<T>() where T : IUIView
    {
        var type = typeof(T);

        if (_panels.TryGetValue(type, out var panel))
        {
            panel.Show();
            return (T)panel;
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
        if (_panels.TryGetValue(typeof(T), out var panel))
            panel.Hide();
    }

    public T Get<T>() where T : IUIView
    {
        if (_panels.TryGetValue(typeof(T), out var panel))
            return (T)panel;
        return default;
    }
}
