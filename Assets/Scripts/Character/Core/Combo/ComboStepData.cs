using UnityEngine;

/// <summary>
/// 连招每段数据 — 可序列化，在 ComboConfigSO 的列表里编辑
/// </summary>
[System.Serializable]
public class ComboStepData
{
    [Header("动画")]
    public string animStateName;                // Animator Controller 里的状态名

    [Header("输入窗口")]
    [Range(0, 1)]
    public float inputWindowStart = 0.4f;       // 动画播到多少才接受预输入

    [Header("音效（可空）")]
    public AudioClip attackSound;               // 攻击音效
    public AudioClip[] voiceClips;              // 角色喊声（随机）

    [Header("特效（可空，预留接口）")]
    public GameObject hitVfxPrefab;             // 受击特效预制体

    [Header("震屏")]
    public float[] shakeForceList;   // 每段多击力度    // 震屏力度（0=不震）
    public float[] hitPauseList;  // 顿帧时长（0=不卡）
    [Range(0f, 1f)] public float hitPauseScale = 0.05f;     // 顿帧缩放
}
