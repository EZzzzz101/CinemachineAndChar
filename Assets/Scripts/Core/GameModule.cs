using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 带单例的模块基类 — 所有 Manager 继承这个
/// 合并了 Singleton + 哈希初始化缓存
///
/// 用法：
///   public class GameStateManager : GameModule<GameStateManager>
///   GameModules.Init() 统一触发初始化
///   GameManager.Instance 全局访问
/// </summary>
public abstract class GameModule<T> : MonoBehaviour where T : GameModule<T>
{
    private static T _instance;
    private static readonly HashSet<Type> _initialized = new();

    // ─── 单例访问 ───

    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<T>();
                if (_instance == null)
                {
                    var go = new GameObject(typeof(T).Name);
                    _instance = go.AddComponent<T>();
                }
            }
            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;

    // ─── 生命周期 ───

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = (T)this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    // ─── 初始化（哈希缓存） ───

    /// <summary>由 GameModules.Init() 统一调用，Type 哈希保证只初始化一次</summary>
    public bool Init()
    {
        var type = GetType();
        if (_initialized.Contains(type)) return false;
        _initialized.Add(type);
        OnInit();
        return true;
    }

    /// <summary>子类写实际初始化逻辑</summary>
    protected abstract void OnInit();

    /// <summary>重置所有模块初始化状态（退出时）</summary>
    public static void ResetAll()
    {
        _initialized.Clear();
    }
}
