using Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 鼠标滚轮缩放 — 挂在 VCam 所在 GameObject 上
/// </summary>
public class CameraZoom : MonoBehaviour
{
    [SerializeField] private float _minDistance = 1f;
    [SerializeField] private float _maxDistance = 8f;
    [SerializeField] private float _zoomSpeed = 4f;
    [SerializeField] private float _sensitivity = 1f;

    private CinemachineVirtualCamera _vCam;
    private CinemachineFramingTransposer _transposer;
    private float _targetDistance;

    void Awake()
    {
        _vCam = GetComponent<CinemachineVirtualCamera>();
        _transposer = _vCam.GetCinemachineComponent<CinemachineFramingTransposer>();
        _targetDistance = _transposer.m_CameraDistance;
    }

    void Update()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f)) return;

        _targetDistance -= scroll * _sensitivity;
        _targetDistance = Mathf.Clamp(_targetDistance, _minDistance, _maxDistance);
    }

    void LateUpdate()
    {
        _transposer.m_CameraDistance = Mathf.Lerp(
            _transposer.m_CameraDistance, _targetDistance, Time.deltaTime * _zoomSpeed);
    }
}
