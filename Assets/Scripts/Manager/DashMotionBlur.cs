using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SingletonTool;

/// <summary>
/// 闪避运动模糊（URP 14）— 闪避时开启 Motion Blur 后期，结束淡出。
/// 单例（Instance 自动创建）：自动在主相机上开启 URP 后处理，并在自身挂全局 Volume + Motion Blur override。
/// DashingState 闪避进/出时调 Play() / Stop()（强度/淡出时长来自 PlayerController 的 Inspector 配置）。
/// </summary>
public class DashMotionBlur : Singleton<DashMotionBlur>
{
    private Volume _volume;
    private MotionBlur _motionBlur;
    private float _current;
    private float _peak;        // 淡出起始强度（线性衰减）
    private bool _fadingOut;
    private float _fadeDuration;

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

        _motionBlur.active = false;
        _motionBlur.intensity.Override(0f);
    }

    /// <summary>闪避开始：按给定强度开启运动模糊</summary>
    public void Play(float amount)
    {
        if (_motionBlur == null) return;
        _peak = Mathf.Clamp01(amount);
        _current = _peak;
        _fadingOut = false;
        _motionBlur.active = true;
        _motionBlur.intensity.Override(_current);
    }

    /// <summary>闪避结束：淡出关闭</summary>
    public void Stop(float fadeDuration)
    {
        if (_motionBlur == null) return;
        _fadeDuration = Mathf.Max(fadeDuration, 0.001f);
        _fadingOut = true;
    }

    void Update()
    {
        if (_motionBlur == null || !_motionBlur.active) return;

        if (_fadingOut)
        {
            // 从峰值线性衰减到 0，用 unscaledDeltaTime 避免时停(timeScale=0)时卡住
            _current = Mathf.Max(0f, _current - (_peak / _fadeDuration) * Time.unscaledDeltaTime);
            _motionBlur.intensity.Override(_current);
            if (_current <= 0f)
            {
                _motionBlur.active = false;
                _fadingOut = false;
            }
        }
    }
}
