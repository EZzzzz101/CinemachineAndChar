using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// UI 总控 — View 用类型做 key 缓存，懒加载
/// </summary>
public class UIManager : GameModule<UIManager>
{
    [Header("场景引用")]
    [SerializeField] private Transform _root;
    [SerializeField] private Canvas _canvas;

    private bool _selfCreatedCanvas;   // 标记当前 Canvas 是自建兜底

    private readonly Dictionary<Type, IUIView> _views = new();

    public Canvas RootCanvas => _canvas;
    public Transform RootTransform => _root;

    protected override void OnInit()
    {
        EnsureRoot();
        EnsureEventSystem();
        Debug.Log("[UIManager] 初始化完成");
    }

    /// <summary>绑定常驻 Canvas</summary>
    public void BindRoot(RectTransform uiCanvas)
    {
        if (uiCanvas == null) return;

        // 若此前已自建兜底 Canvas，换成场景注册的，避免双 Canvas
        if (_selfCreatedCanvas && _canvas != null && _canvas.gameObject != uiCanvas.gameObject)
        {
            Destroy(_canvas.gameObject);
            _selfCreatedCanvas = false;
        }

        _canvas = uiCanvas.GetComponent<Canvas>();
        _root = uiCanvas;
        Debug.Log($"[UIManager] 绑定常驻 Canvas: {uiCanvas.name}");
    }

    /// <summary>保证有常驻 UI 根：优先用注册的 Canvas，没有则自建一个（面板永远有根，不会挂到场景根）</summary>
    public void EnsureRoot()
    {
        if (_canvas != null) return;

        var go = new GameObject("UICanvas");
        DontDestroyOnLoad(go);

        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600);   // 与 Boot Canvas 一致
        scaler.matchWidthOrHeight = 0f;

        go.AddComponent<GraphicRaycaster>();

        _root = go.transform;
        _selfCreatedCanvas = true;
        Debug.Log("[UIManager] 未找到常驻 Canvas，已自建 UICanvas");
    }

    /// <summary>
    /// 保证场景里有 EventSystem，否则 UI 点击收不到事件（直接从非启动场景 Play 时常见）。
    /// 已存在则不重复创建。
    /// </summary>
    private void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        DontDestroyOnLoad(go);
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();   // 无引用时自动 AssignDefaultActions
        Debug.Log("[UIManager] 场景缺少 EventSystem，已自动创建");
    }

    public void Register(IUIView view)
    {
        _views[view.GetType()] = view;
    }

    public T Open<T>() where T : IUIView
    {
        var type = typeof(T);

        if (_views.TryGetValue(type, out var view) && IsViewValid(view))
        {
            view.Show();
            return (T)view;
        }

        // 缓存里可能残留"已销毁"的引用（Unity 假 null），清掉再走异步加载
        _views.Remove(type);

        // 未实例化过：异步从资源提供者加载（AB/编辑器兜底），调用方不需要返回值
        OpenAsync<T>().Forget();
        return default;
    }

    /// <summary>异步打开面板：优先缓存，没有则从资源提供者加载预制体实例化</summary>
    public async UniTask<T> OpenAsync<T>() where T : IUIView
    {
        var type = typeof(T);

        if (_views.TryGetValue(type, out var view) && IsViewValid(view))
        {
            view.Show();
            return (T)view;
        }

        _views.Remove(type);

        EnsureRoot();   // 兜底：保证 _root 指向常驻 Canvas，而不是 FindObjectOfType 乱找
        EnsureEventSystem();

        var prefab = await ResourceManager.Instance.LoadAsync<GameObject>($"UI/Panels/{type.Name}");
        if (prefab != null)
        {
            var go = Instantiate(prefab, _root);
            go.SetActive(true);
            return go.GetComponent<T>();
        }

        Debug.LogWarning($"[UIManager] Open<{type.Name}> 失败：UI/Panels/{type.Name} 预制体不存在");
        return default;
    }

    public void Close<T>() where T : IUIView
    {
        if (_views.TryGetValue(typeof(T), out var view) && IsViewValid(view))
            view.Hide();
    }

    public T Get<T>() where T : IUIView
    {
        if (_views.TryGetValue(typeof(T), out var view) && IsViewValid(view))
            return (T)view;
        return default;
    }

    /// <summary>
    /// 校验缓存的 View 是否还活着：Unity 对象被 Destroy 后，接口引用 != null 但转成
    /// UnityEngine.Object 再比 null 能识别出来（Unity 重载了 ==）。
    /// </summary>
    private static bool IsViewValid(IUIView view)
    {
        return view != null && view as UnityEngine.Object != null;
    }
}
