using UnityEngine;

/// <summary>
/// 相机管理器（模块）— 移动方向计算（相对相机）
/// 继承 GameModule&lt;CameraManager&gt;，由 GameModules.Init() 统一初始化并常驻；
/// 没在场景里放对象也会自动创建，避免角色转向 NRE。
/// VCam 的 Follow/LookAt 自动处理旋转。
/// </summary>
public class CameraManager : GameModule<CameraManager>
{
    protected override void OnInit()
    {
        Debug.Log("[CameraManager] 初始化完成");
    }

    public Vector3 GetMoveDir(Vector2 input)
    {
        Transform cam = Camera.main.transform;
        Vector3 forward = cam.forward; forward.y = 0;
        Vector3 right   = cam.right;   right.y = 0;
        return (forward * input.y + right * input.x).normalized;
    }
}
