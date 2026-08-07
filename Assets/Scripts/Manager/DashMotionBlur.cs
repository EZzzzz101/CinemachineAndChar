using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SingletonTool;

/// <summary>
/// 完美闪避运动模糊（URP 14）— 按"快速冲顶 → 略回落 → 时停保持 → 曲线淡出"的强度包络播放。
/// 单例（Instance 自动创建）：自动在主相机上开启 URP 后处理，并在自身挂全局 Volume + Motion Blur override。
/// PlayerController 完美闪避时调 Play(intensity, duration, fadeDuration)，播放完自动关闭。
/// </summary>
public class DashMotionBlur : Singleton<DashMotionBlur>
{
    // ===== 强度包络（真实秒，可按手感调）=====
    private const float AttackTime = 0.06f;   // 触发后冲到峰值的时间（越短越"撞"）
    private const float SettleTime = 0.22f;   // 冲到顶后回落到"保持值"的时刻
    private const float HoldLevel = 0.78f;    // 时停期间保持的相对强度（0~1）

    private Volume _volume;
    private MotionBlur _motionBlur;
    private float _peak;         // 峰值强度（调用方传入，0~1）
    private float _duration;     // 时停时长（真实秒）
    private float _fadeDuration;
    private float _elapsed;      // 已播放时长（真实秒）

    protected override void Awake()
    {
        base.Awake();
        Setup();
    }

    private void Setup()
    {
        // 主相机开启 URP 后处理（否则 Volume override 不生效）
        var cam = Camera.main;
        if (cam != null)
        {
            var urp = cam.GetUniversalAdditionalCameraData();
            if (urp != null) urp.renderPostProcessing = true;
        }

        // 全局 Volume + Motion Blur override（挂在单例物体上，全局生效）
        _volume = GetComponent<Volume>();
        if (_volume == null) _volume = gameObject.AddComponent<Volume>();
        _volume.isGlobal = true;

        var profile = _volume.profile;
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _volume.profile = profile;
        }
        if (!profile.TryGet<MotionBlur>(out _motionBlur))
            _motionBlur = profile.Add<MotionBlur>(true);

        // 高质量采样 + 更长拖影，让模糊更有"动态感"（URP 默认 Low 太糊太平）
        _motionBlur.active = false;
        _motionBlur.quality.Override(MotionBlurQuality.High);
        _motionBlur.clamp.Override(0.08f);
        _motionBlur.intensity.Override(0f);
    }

    /// <summary>
    /// 完美闪避：按强度包络播放。
    /// </summary>
    /// <param name="amount">峰值强度(0~1)</param>
    /// <param name="duration">时停时长（真实秒），期间保持模糊强度</param>
    /// <param name="fadeDuration">淡出时长（真实秒），曲线收尾</param>
    public void Play(float amount, float duration, float fadeDuration)
    {
        if (_motionBlur == null) return;
        _peak = Mathf.Clamp01(amount);
        _duration = Mathf.Max(duration, 0f);
        _fadeDuration = Mathf.Max(fadeDuration, 0.001f);
        _elapsed = 0f;
        _motionBlur.active = true;
        _motionBlur.intensity.Override(0f);
    }

    void Update()
    {
        if (_motionBlur == null || !_motionBlur.active) return;

        _elapsed += Time.unscaledDeltaTime;   // 时停(timeScale<1)期间仍按真实时间推进
        _motionBlur.intensity.Override(_peak * Evaluate(_elapsed));

        if (_elapsed >= _duration + _fadeDuration)
        {
            _motionBlur.active = false;
            _motionBlur.intensity.Override(0f);
        }
    }

    /// <summary>强度包络：快速冲顶 → 略回落 → 时停保持 → 曲线淡出</summary>
    private float Evaluate(float t)
    {
        // 1) 触发瞬间快速冲顶（先快后慢，撞出力量感）
        if (t <= AttackTime)
        {
            float u = t / AttackTime;
            return 1f - (1f - u) * (1f - u);   // OutQuad
        }

        // 2) 从峰值略回落并稳住（快速掉头、减速落地）
        if (t <= SettleTime)
        {
            float u = (t - AttackTime) / (SettleTime - AttackTime);
            return Mathf.Lerp(1f, HoldLevel, u * u);   // InQuad
        }

        // 3) 时停期间保持强度
        if (t <= _duration)
            return HoldLevel;

        // 4) 淡出：先快后缓（OutCubic），收得干净
        float ft = Mathf.Clamp01((t - _duration) / _fadeDuration);
        return Mathf.Lerp(HoldLevel, 0f, 1f - Mathf.Pow(1f - ft, 3f));
    }
}
