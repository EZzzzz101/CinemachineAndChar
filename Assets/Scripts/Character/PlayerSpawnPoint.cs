using UnityEngine;
using Cinemachine;

/// <summary>
/// 玩家出生点 — 挂在场景里的空物体上（如六分街/战斗场景）。
/// 进场景后实例化玩家 prefab，并把场景里的自由 VCam 的 Follow/LookAt 绑到玩家。
/// 这样角色不用复制进每个场景，换场景只换出生点位置。
/// </summary>
public class PlayerSpawnPoint : MonoBehaviour
{
    [Header("玩家")]
    [Tooltip("玩家 prefab（如 安比.prefab）")]
    [SerializeField] private GameObject playerPrefab;

    [Header("相机")]
    [Tooltip("自由相机 VCam；留空自动找场景里的第一个 CinemachineVirtualCamera")]
    [SerializeField] private CinemachineVirtualCamera freeCamera;

    [Header("邦布")]
    [Tooltip("邦布 prefab（可空，不生成）")]
    [SerializeField] private GameObject bangbooPrefab;

    [Tooltip("邦布出生位置相对出生点的偏移")]
    [SerializeField] private Vector3 bangbooOffset = new Vector3(-0.5f, 0f, 0.5f);

    private void Start()
    {
        if (playerPrefab == null)
        {
            Debug.LogWarning($"[PlayerSpawnPoint] {name}: 未指定玩家 prefab，跳过生成");
            return;
        }

        // 实例化玩家到出生点
        GameObject player = Instantiate(playerPrefab, transform.position, transform.rotation);
        player.name = playerPrefab.name;   // 去掉 (Clone)，方便识别

        // 角色生成后再生成邦布，并把 target 显式指向角色（安比 tag 是 Untagged，不能靠自动查找）
        SpawnBangboo(player.transform);

        // 相机跟随玩家（玩家是运行时生成的，Follow/LookAt 必须在生成后绑定）
        Transform cameraPoint = FindCameraPoint(player.transform);
        if (freeCamera == null)
            freeCamera = FindObjectOfType<CinemachineVirtualCamera>();
        if (freeCamera != null)
        {
            freeCamera.Follow = cameraPoint;
            freeCamera.LookAt = cameraPoint;
        }
    }

    private void SpawnBangboo(Transform player)
    {
        if (bangbooPrefab == null) return;

        GameObject bangboo = Instantiate(bangbooPrefab, transform.position + bangbooOffset, Quaternion.identity);
        bangboo.name = bangbooPrefab.name;

        var follow = bangboo.GetComponent<BangbooFollow>();
        if (follow != null) follow.SetTarget(player);

        var brain = bangboo.GetComponent<BangbooBrain>();
        if (brain != null) brain.SetTarget(player);
    }

    /// <summary>按名字找角色身上的相机锚点子物体（如 CameraBasePoint），找不到退回根节点</summary>
    private Transform FindCameraPoint(Transform playerRoot)
    {
        foreach (var t in playerRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "CameraBasePoint")
                return t;
        }
        return playerRoot;
    }
}
