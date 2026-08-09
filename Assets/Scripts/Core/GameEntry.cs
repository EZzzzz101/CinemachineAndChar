using UnityEngine;
using Cysharp.Threading.Tasks;

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
        // 资源引导挂启动器：与读条 UI 解耦（LoadingFlow 只订阅进度显示，不驱动流程）。
        // RunFlow 幂等：BootFlow/其他兜底重复调用无害。
        HotUpdateManager.Instance.RunFlow().Forget();
        Debug.Log("[GameEntry] 所有模块初始化完成");
    }
}
