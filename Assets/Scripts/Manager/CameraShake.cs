using Cinemachine;
using UnityEngine;
using SingletonTool;

/// <summary>
/// 相机震屏管理器（单例）— 对 CinemachineImpulseSource 的轻量封装
/// ImpulseSource 波形/包络等参数直接在 Inspector 里设
/// </summary>
public class CameraShake : Singleton<CameraShake>
{
    private CinemachineImpulseSource _impulseSource;

    protected override void Awake()
    {
        base.Awake();

        _impulseSource = GetComponent<CinemachineImpulseSource>();
        if (_impulseSource == null)
            _impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();

        if (CinemachineImpulseManager.Instance != null)
            CinemachineImpulseManager.Instance.IgnoreTimeScale = true;
    }

    public void TriggerShake(float force)
    {
        _impulseSource?.GenerateImpulseWithForce(force);
    }
}
