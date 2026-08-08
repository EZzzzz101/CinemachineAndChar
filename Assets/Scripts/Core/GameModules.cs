

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

        // 1.5 热更 — 版本检查/下载/切 provider 的实际流程由 BootFlow 触发，这里只注册模块
        HotUpdateManager.Instance.Init();

        // 2. 游戏状态
        GameStateManager.Instance.Init();

        // 3. UI 系统（依赖 ResourceManager 加载面板预制体）
        UIManager.Instance.Init();

        // 4. 相机（移动方向计算，常驻）
        CameraManager.Instance.Init();

        // 5. 场景加载（loadingUI 引用等配置随模块初始化就绪）
        SceneLoader.Instance.Init();

        // 6. 背景音乐（常驻，跨场景按场景切曲）
        BgmManager.Instance.Init();

        // 7. 战斗 UI（进战斗场景自动从 Resources 加载 HUD / 预加载胜利面板）
        BattleUILoader.Instance.Init();

    }

    /// <summary>退出时清理（场景切换时不需要调）</summary>
    public static void Shutdown()
    {
        if (!_initialized) return;
        _initialized = false;

        EventBus.Clear();
    }
}
