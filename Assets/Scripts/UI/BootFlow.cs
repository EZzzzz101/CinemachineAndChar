using UnityEngine;

/// <summary>
/// 启动流程（真实版）— 挂在 Boot 场景（Canvas 或 LoadingBar 上）。
/// 跑热更真实流程：版本检查 → 下载 AB → provider 切换 → 预加载关键资源，
/// 每阶段把真实进度喂给 LoadingFlow.SetProgress；完成后由 LoadingFlow 隐藏读条并打开登录窗口。
/// </summary>
public class BootFlow : MonoBehaviour
{
    [Tooltip("读条流程脚本（挂在 LoadingBar 上）；留空自动查找")]
    [SerializeField] private LoadingFlow loadingFlow;

    private void Start()
    {
        // 资源引导已由 GameEntry 驱动（RunFlow 幂等），本脚本不再重复调用，避免读条跳满。
        // 读条显示由 LoadingFlow 直接订阅 HotUpdateManager 的进度/状态事件。
        // 本脚本保留仅为兼容 Boot 场景的组件挂载；如需自定义 UI 转发，请订阅 ProgressChanged/StatusChanged。
    }

    private void OnHotUpdateStatus(string status)
    {
        if (loadingFlow != null)
            loadingFlow.SetStatus(status);
    }

    private void OnDestroy()
    {
        if (HotUpdateManager.HasInstance)
            HotUpdateManager.Instance.StatusChanged -= OnHotUpdateStatus;
    }
}
