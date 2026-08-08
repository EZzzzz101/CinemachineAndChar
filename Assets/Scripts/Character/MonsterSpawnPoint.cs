using UnityEngine;
using Cysharp.Threading.Tasks;

/// <summary>
/// 怪兽出生点 — 挂在战斗场景里的空物体上，位置由场景自己摆。
/// 进场景后从 Resources 加载怪兽 prefab 并在自身位置实例化。
/// 玩家/邦布由 PlayerSpawnPoint 生成。
/// </summary>
public class MonsterSpawnPoint : MonoBehaviour
{
    [Header("怪兽")]
    [Tooltip("怪兽 prefab（可空，留空则按下方 Resources 路径加载）")]
    [SerializeField] private GameObject monsterPrefab;
    [Tooltip("怪兽 prefab 的 Resources 路径（Assets/Resources 下）")]
    [SerializeField] private string monsterPrefabPath = "Prefabs/怪兽";

    private async void Start()
    {
        var source = monsterPrefab != null ? monsterPrefab : await ResourceManager.Instance.LoadAsync<GameObject>(monsterPrefabPath);
        if (source == null)
        {
            Debug.LogWarning($"[MonsterSpawnPoint] {name}: 未找到怪兽 prefab（拖引用或 {monsterPrefabPath}），跳过生成");
            return;
        }

        var go = Instantiate(source, transform.position, transform.rotation);
        go.name = source.name;   // 去掉 (Clone)
    }
}
