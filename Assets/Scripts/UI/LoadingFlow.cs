using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 读条流程 — 挂在 Boot 场景的 LoadingBar（或 Canvas）上。
/// 读条到 100% 后：隐藏 LoadingBar，用 UIManager 打开登录窗口 GameLaunchView。
/// 用法：读条逻辑每帧调 SetProgress(0~1)，满 1 自动完成；或直接调 Complete()。
/// </summary>
public class LoadingFlow : MonoBehaviour
{
    [Header("读条")]
    [Tooltip("读条根物体，完成后隐藏")]
    [SerializeField] private GameObject loadingBar;

    [Tooltip("fill 子物体的 Image（可选）：Filled 类型用 fillAmount，拉伸类型用横向缩放")]
    [SerializeField] private Image loadingBarFill;

    private bool _completed;

    /// <summary>读条进度 0~1；到 1 自动进入完成流程</summary>
    public void SetProgress(float progress)
    {
        if (_completed) return;

        float p = Mathf.Clamp01(progress);
        if (loadingBarFill != null)
        {
            if (loadingBarFill.type == Image.Type.Filled)
                loadingBarFill.fillAmount = p;
            else
            {
                var rt = loadingBarFill.rectTransform;
                rt.localScale = new Vector3(p, rt.localScale.y, rt.localScale.z);
            }
        }

        if (p >= 1f) Complete();
    }

    /// <summary>读条完成：隐藏读条 + 打开登录窗口（幂等，只执行一次）</summary>
    public void Complete()
    {
        if (_completed) return;
        _completed = true;

        if (loadingBar != null)
            loadingBar.SetActive(false);

        UIManager.Instance.Open<GameLaunchView>();
    }
}
