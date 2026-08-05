using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 一键给场景加「地面层 + 角色碰撞」
/// 菜单: Tools/场景/一键地面层+碰撞
///
/// 只做两件事(碰撞体本身来自街道 FBX 的导入设置 addColliders):
/// 1. 给 tag=Player 的角色加 CharacterController、删掉没用的 Rigidbody;
/// 2. 把街道根物体及其子物体挂到 Ground 层(供代码用射线识别地面)。
/// 顺带删除场景里失效的 Plane (1)。
/// </summary>
public static class SceneGroundSetup
{
    [MenuItem("Tools/场景/一键地面层+碰撞")]
    public static void Setup()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded)
        {
            Debug.LogError("[SceneGroundSetup] 请先打开要处理的场景");
            return;
        }

        SetupPlayer();
        SetupGroundLayer();
        CleanupStrayPlane();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("[SceneGroundSetup] 完成 ✓ (若角色悬空/陷地,请在场景里微调安比的 Y,或改 PlayerController 的 _groundSnapSpeed)");
    }

    // 1) 角色碰撞:删 Rigidbody → 加 CharacterController(不碰 applyRootMotion)
    private static void SetupPlayer()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[SceneGroundSetup] 没找到 tag=Player 的角色,跳过角色碰撞");
            return;
        }

        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Undo.DestroyObjectImmediate(rb);
            Debug.Log("[SceneGroundSetup] 已删除角色上未使用的 Rigidbody");
        }

        var cc = player.GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = Undo.AddComponent<CharacterController>(player);
            cc.height      = 1.8f;
            cc.radius      = 0.4f;
            cc.center      = new Vector3(0f, 0.9f, 0f); // 胶囊底在脚底 y=0
            cc.stepOffset  = 0.3f;                      // 能上台阶/路沿
            cc.slopeLimit  = 45f;
            Debug.Log($"[SceneGroundSetup] 已给 {player.name} 添加 CharacterController");
        }
        else
        {
            Debug.Log($"[SceneGroundSetup] {player.name} 已有 CharacterController,跳过");
        }
    }

    // 2) 街道 → Ground 层(有碰撞体 ≠ 在 Ground 层;这里只是给代码打地面标签)
    private static void SetupGroundLayer()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0)
        {
            Debug.LogError("[SceneGroundSetup] 找不到名为 Ground 的层,请检查 Project Settings → Tags and Layers");
            return;
        }

        var street = FindStreetRoot();
        if (street == null)
        {
            Debug.LogWarning("[SceneGroundSetup] 没找到街道(SixthStreet),跳过 Ground 层设置");
            return;
        }

        foreach (Transform t in street.GetComponentsInChildren<Transform>(true))
        {
            Undo.RecordObject(t.gameObject, "Ground Layer");
            t.gameObject.layer = groundLayer;
        }
        Debug.Log($"[SceneGroundSetup] 已把 {street.name} 及其子物体设为 Ground 层({groundLayer})");
    }

    // 3) 清理失效的 Plane (1)
    private static void CleanupStrayPlane()
    {
        var plane = GameObject.Find("Plane (1)");
        if (plane == null)
        {
            Debug.Log("[SceneGroundSetup] 未找到 Plane (1),跳过清理");
            return;
        }
        Undo.DestroyObjectImmediate(plane);
        Debug.Log("[SceneGroundSetup] 已删除场景里失效的 Plane (1)");
    }

    private static GameObject FindStreetRoot()
    {
        var found = GameObject.Find("SixthStreet4.6");
        if (found != null) return found;

        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name.Contains("SixthStreet"))
                return root;
        }
        return null;
    }
}
