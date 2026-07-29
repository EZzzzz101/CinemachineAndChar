

using UnityEngine;


/// <summary>
/// 模块统一入口 — 集中管理所有模块的初始化顺序
///
/// 在 GameEntry 中调用一次即可
/// 各模块继承 GameModule<T>，Init() 由这里统一触发
/// </summary>
public static class GameModules
{
    private static bool _initialized;

    /// <summary>按依赖顺序初始化所有模块</summary>
    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        // 1. 资源 — 基础，供后续加载用
        ResourceManager.Instance.Init();

        // 2. 游戏状态
        GameStateManager.Instance.Init();

        // 3. UI 系统（依赖 ResourceManager 加载面板预制体）
        UIManager.Instance.Init();

    }

    /// <summary>退出时清理（场景切换时不需要调）</summary>
    public static void Shutdown()
    {
        if (!_initialized) return;
        _initialized = false;

        EventBus.Clear();
    }
}
