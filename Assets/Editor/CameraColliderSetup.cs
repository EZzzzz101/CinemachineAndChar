using Cinemachine;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 给第三人称 VCam 加 CinemachineCollider,防止相机穿墙 / 穿地
/// 菜单: Tools/场景/相机防穿模
///
/// 原理:Collider 扩展会对「Follow 目标 → 期望相机位置」做 SphereCast,
/// 撞到静态障碍就把相机沿视线拉回,贴地时把相机抬到地面之上。
/// </summary>
public static class CameraColliderSetup
{
    [MenuItem("Tools/场景/相机防穿模")]
    public static void Setup()
    {
        // 场景里所有 VCam 都挂上(以后加战斗机位也一起被保护)
        var vcams = Object.FindObjectsByType<CinemachineVirtualCamera>(FindObjectsSortMode.None);
        if (vcams.Length == 0)
        {
            Debug.LogError("[CameraColliderSetup] 场景里没找到 CinemachineVirtualCamera");
            return;
        }

        foreach (var vcam in vcams)
        {
            var collider = vcam.GetComponent<CinemachineCollider>();
            if (collider == null)
            {
                Undo.AddComponent<CinemachineCollider>(vcam.gameObject);
                collider = vcam.GetComponent<CinemachineCollider>();
                Debug.Log($"[CameraColliderSetup] 已给 {vcam.name} 添加 CinemachineCollider");
            }
            else
            {
                Debug.Log($"[CameraColliderSetup] {vcam.name} 已有 CinemachineCollider,更新参数");
            }

            Undo.RecordObject(vcam.gameObject, "Camera Collider Params");
            ApplySettings(collider);
        }

        EditorUtility.SetDirty(vcams[0].gameObject);
        AssetDatabase.SaveAssets();
        Debug.Log("[CameraColliderSetup] 完成 ✓ 进 Play 试一下;穿得还不够 → 调小 CameraRadius,太弹 → 调大两个 Damping");
    }

    private static void ApplySettings(CinemachineCollider c)
    {
        // 2.10.7 的 CinemachineCollider 字段都是公开字段,直接赋值
        c.m_CollideAgainst            = ~0;   // 所有层都挡(街道在 Ground 层也覆盖)
        c.m_IgnoreTag                 = "Player"; // 忽略角色自己,否则会一直和安比的 CC 碰撞
        c.m_AvoidObstacles            = true;
        c.m_MinimumDistanceFromTarget = 0.1f; // 相机离角色最近距离
        c.m_CameraRadius              = 0.12f;// 小一点 → 贴墙更紧,看起来更自然
        c.m_Strategy                  = CinemachineCollider.ResolutionStrategy.PullCameraForward;
        c.m_MaximumEffort             = 4;    // 每帧最多几次球扫,太大伤性能
        c.m_Damping                   = 0.2f; // 正常跟随时平滑
        c.m_DampingWhenOccluded       = 0.6f; // 被墙挡住推进/退出时平滑,防抖动
    }
}
