using UnityEngine;
using Cinemachine;
using Cysharp.Threading.Tasks;

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

    private async void Start()
    {
        // 服务器权威：客户端角色（非房主）不由场景 PlayerSpawnPoint 生成，
        // 改由 BattleClientRuntime 按服务器下发的出生点生成（避免"先生成再搬"）。
        if (BattleSessionState.FromLobby && !BattleSessionState.IsHost)
        {
            Debug.Log("[BattleFlow] 客户端角色：跳过 PlayerSpawnPoint 生成，等待服务器出生点");
            return;
        }
        // 兜底：客户端进程（存在 BattleClientRuntime）一律不由 PlayerSpawnPoint 生成，
        // 防止 FromLobby 因时序未填充时重复生成本地玩家。
        if (FindObjectOfType<BattleClientRuntime>() != null)
        {
            Debug.Log("[BattleFlow] 检测到客户端运行时：跳过 PlayerSpawnPoint 生成");
            return;
        }

        await SpawnPlayerAt(transform.position);
    }

    /// <summary>
    /// 在指定位置生成玩家并完成绑定（相机/LockOn/PlayerSpawned 事件）。
    /// 房主由 Start 调用（出生点位置）；客户端由 BattleClientRuntime 调用（服务器下发的出生点）。
    /// </summary>
    public async UniTask<GameObject> SpawnPlayerAt(Vector3 position)
    {
        // 实例化玩家到出生点
        GameObject player = await SpawnAsync(playerPrefab, playerPrefabPath, position, transform.rotation);
        if (player == null)
        {
            Debug.LogWarning($"[PlayerSpawnPoint] {name}: 未找到玩家 prefab（拖引用或 {playerPrefabPath}），跳过生成");
            return null;
        }

        // 邦布（可选）
        await SpawnBangboo(player.transform);

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
        return player;
    }

    /// <summary>优先用 Inspector 引用，空则按地址走资源提供者加载并实例化</summary>
    private async UniTask<GameObject> SpawnAsync(GameObject prefab, string path, Vector3 position, Quaternion rotation)
    {
        var source = prefab != null ? prefab : await ResourceManager.Instance.LoadAsync<GameObject>(path);
        if (source == null) return null;

        var go = Instantiate(source, position, rotation);
        go.name = source.name;   // 去掉 (Clone)，方便识别
        return go;
    }

    private async UniTask SpawnBangboo(Transform player)
    {
        if (!spawnBangboo) return;

        GameObject bangboo = await SpawnAsync(bangbooPrefab, bangbooPrefabPath, transform.position + bangbooOffset, Quaternion.identity);
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
