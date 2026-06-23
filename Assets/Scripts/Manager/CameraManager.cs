using UnityEngine;

/// <summary>
/// 相机管理器（单例）— 移动方向计算
/// VCam 的 Follow/LookAt 自动处理旋转
/// </summary>
public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    public Vector3 GetMoveDir(Vector2 input)
    {
        Transform cam = Camera.main.transform;
        Vector3 forward = cam.forward; forward.y = 0;
        Vector3 right   = cam.right;   right.y = 0;
        return (forward * input.y + right * input.x).normalized;
    }
}
