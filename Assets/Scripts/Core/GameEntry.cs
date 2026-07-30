using UnityEngine;

/// <summary>
/// 游戏入口 — 挂载在启动场景的 GameObject 上
/// 游戏启动时初始化所有模块
/// </summary>
public class GameEntry : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        GameModules.Init();
        Debug.Log("[GameEntry] 所有模块初始化完成");
    }
}
