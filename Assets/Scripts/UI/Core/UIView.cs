using UnityEngine;

/// <summary>
/// View 基类 — 实现 IUIView，所有 UI 面板继承这个
/// Awake 时自动注册到 UIManager
/// </summary>
public abstract class UIView : MonoBehaviour, IUIView
{
    protected virtual void Awake()
    {
        UIManager.Instance.Register(this);
    }

    public virtual void Show() { gameObject.SetActive(true); }
    public virtual void Hide() { gameObject.SetActive(false); }
}
