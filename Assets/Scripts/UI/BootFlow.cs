using System.Collections;
using UnityEngine;

/// <summary>
/// 启动流程（模拟版）— 挂在 Boot 场景（Canvas 或 LoadingBar 上）。
/// 协程模拟读条 0→100%，完成后经 LoadingFlow 隐藏读条并打开登录窗口 GameLaunchView。
/// 后续替换成真实流程：热更检查 → 模块初始化 → 资源预加载（每阶段往 LoadingFlow.SetProgress 喂进度）。
/// </summary>
public class BootFlow : MonoBehaviour
{
    [Header("读条模拟")]
    [Tooltip("模拟读条总时长（秒），0→100%")]
    [SerializeField] private float simulateDuration = 2.5f;

    [Tooltip("读条流程脚本（挂在 LoadingBar 上）；留空自动查找")]
    [SerializeField] private LoadingFlow loadingFlow;

    private void Start()
    {
        if (loadingFlow == null)
            loadingFlow = GetComponent<LoadingFlow>() ?? FindObjectOfType<LoadingFlow>();

        StartCoroutine(SimulateLoading());
    }

    private IEnumerator SimulateLoading()
    {
        float duration = Mathf.Max(simulateDuration, 0.1f);
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            if (loadingFlow != null)
                loadingFlow.SetProgress(t / duration);   // 0 → 100%
            yield return null;
        }

        // 满 100%：LoadingFlow 内部自动隐藏读条 + UIManager.Open<GameLaunchView>()
        if (loadingFlow != null)
            loadingFlow.SetProgress(1f);
    }
}
