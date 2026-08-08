using UnityEngine;
using Cinemachine;

/// <summary>
/// 玩家出生点 — 挂在场景里的空物体上（六分街/战斗场景）。
/// 进场景后从 prefab 实例化玩家（优先 Inspector 引用，空则按 Resources 路径加载），
/// 绑定自由相机 Follow/LookAt、邦布 target、并把玩家显式注入 LockOnManager 与 PlayerSpawned 事件。
/// 角色不用复制进每个场景，换场景只换出生点位置。
/// </summary>
public class PlayerSpawnPoint : MonoBehaviour
{
    [Header("玩家")]
    [Tooltip("玩家 prefab（可空，留空则按下方 Resources 路径加载）")]
    [SerializeField] private GameObject playerPrefab;
    [Tooltip("玩家 prefab 的 Resources 路径（Assets/Resources 下）")]
    [SerializeField] private string playerPrefabPath = "Prefabs/安比";

    [Header("相机")]
    [Tooltip("自由相机 VCam；留空自动找场景里的第一个 CinemachineVirtualCamera")]
    [SerializeField] private CinemachineVirtualCamera freeCamera;

    [Header("邦布")]
    [Tooltip("是否生成邦布（战斗场景建议关，省得联机多同步一个位置）")]
    [SerializeField] private bool spawnBangboo = true;
    [Tooltip("邦布 prefab（可空，留空则按下方 Resources 路径加载）")]
    [SerializeField] private GameObject bangbooPrefab;
    [Tooltip("邦布 prefab 的 Resources 路径")]
    [SerializeField] private string bangbooPrefabPath = "Prefabs/Bangboo";
    [Tooltip("邦布出生位置相对出生点的偏移")]
    [SerializeField] private Vector3 bangbooOffset = new Vector3(-0.5f, 0f, 0.5f);

    private void Start()
    {
        // 实例化玩家到出生点
        GameObject player = Spawn(playerPrefab, playerPrefabPath, transform.position, transform.rotation);
        if (player == null)
        {
            Debug.LogWarning($"[PlayerSpawnPoint] {name}: 未找到玩家 prefab（拖引用或 Resources/{playerPrefabPath}），跳过生成");
            return;
        }

        // 邦布（可选）
        SpawnBangboo(player.transform);

        // 相机跟随玩家：运行时生成，Follow/LookAt 必须在生成后绑定（跟胸口锚点）
        Transform cameraPoint = EnsureCameraPoint(player.transform);
        if (freeCamera == null)
            freeCamera = FindObjectOfType<CinemachineVirtualCamera>();
        if (freeCamera != null)
        {
            freeCamera.Follow = cameraPoint;
            freeCamera.LookAt = cameraPoint;
        }

        // 锁敌系统显式注入玩家（安比 tag 是 Untagged，不能靠 tag 查找）
        if (LockOnManager.HasInstance)
            LockOnManager.Instance.BindPlayer(player.transform);

        // 通知其他系统（如 ChatInputUI）：玩家已生成
        EventBus.Emit(GameEvents.PlayerSpawned, player);
    }

    /// <summary>优先用 Inspector 引用，空则按 Resources 路径加载并实例化</summary>
    private GameObject Spawn(GameObject prefab, string path, Vector3 position, Quaternion rotation)
    {
        var source = prefab != null ? prefab : Resources.Load<GameObject>(path);
        if (source == null) return null;

        var go = Instantiate(source, position, rotation);
        go.name = source.name;   // 去掉 (Clone)，方便识别
        return go;
    }

    private void SpawnBangboo(Transform player)
    {
        if (!spawnBangboo) return;

        GameObject bangboo = Spawn(bangbooPrefab, bangbooPrefabPath, transform.position + bangbooOffset, Quaternion.identity);
        if (bangboo == null) return;

        var follow = bangboo.GetComponent<BangbooFollow>();
        if (follow != null) follow.SetTarget(player);

        var brain = bangboo.GetComponent<BangbooBrain>();
        if (brain != null) brain.SetTarget(player);
    }

    /// <summary>确保有胸口相机锚点：找到 CameraBasePoint 直接返回；没有则在根下创建一个（1.2m 胸口高度）</summary>
    private Transform EnsureCameraPoint(Transform playerRoot)
    {
        foreach (var t in playerRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "CameraBasePoint")
                return t;
        }

        var go = new GameObject("CameraBasePoint");
        go.transform.SetParent(playerRoot, false);
        go.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        return go.transform;
    }
}
