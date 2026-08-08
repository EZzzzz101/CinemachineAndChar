using Cysharp.Threading.Tasks;
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

    private async void Start()
    {
        if (loadingFlow == null)
            loadingFlow = GetComponent<LoadingFlow>() ?? FindObjectOfType<LoadingFlow>();

        // 状态文案：热更阶段 → LoadingFlow 的 StatusText（新版本→更新中，否则→正在进入游戏）
        var hotUpdate = HotUpdateManager.Instance;
        hotUpdate.StatusChanged += OnHotUpdateStatus;

        // 真实流程：版本检查 → 下载 AB → 预加载关键资源（HotUpdateManager 内部喂 0→1）
        var progress = new System.Progress<float>(p =>
        {
            if (loadingFlow != null)
                loadingFlow.SetProgress(p);
        });
        await HotUpdateManager.Instance.RunFlow(progress);

        // 满 100%：LoadingFlow 内部自动隐藏读条 + UIManager.Open<GameLaunchView>()
        if (loadingFlow != null)
            loadingFlow.SetProgress(1f);
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
