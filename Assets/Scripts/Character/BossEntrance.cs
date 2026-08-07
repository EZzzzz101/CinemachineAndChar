using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Boss 房入口 — 挂在六分街的入口空物体上（可穿过触发器）。
/// - 自动补/开启 Trigger 碰撞体（玩家走进不挡路）；
/// - 玩家进入显示提示（按 F 键 我要战斗），离开隐藏；
/// - 按 F 进入战斗场景；
/// - 可选：入口常驻特效（VFXPool 生成，标记"这是 Boss 房"）。
/// </summary>
public class BossEntrance : MonoBehaviour
{
    [Header("入口")]
    [Tooltip("进入后加载的战斗场景名")]
    [SerializeField] private string battleSceneName = "Main";

    [Header("提示")]
    [Tooltip("提示 TMP_Text（可空：自动找名为 BossPromptText 的对象）")]
    [SerializeField] private TMP_Text promptText;

    [Tooltip("提示内容")]
    [SerializeField] private string promptMessage = "按 F 键 我要战斗";

    [Header("特效")]
    [Tooltip("入口常驻特效 prefab（可空；也可以直接把特效拖进场景）")]
    [SerializeField] private GameObject entranceVfxPrefab;

    private bool _playerInside;

    private void Awake()
    {
        // 保证是可穿过的触发器
        var colliders = GetComponents<Collider>();
        if (colliders == null || colliders.Length == 0)
        {
            var col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
        }
        else
        {
            foreach (var c in colliders)
                c.isTrigger = true;
        }

        if (promptText == null)
        {
            var go = GameObject.Find("BossPromptText");
            if (go != null)
                promptText = go.GetComponent<TMP_Text>();
        }
    }

    private void Start()
    {
        // 入口特效：常驻播放（lifetime=0 由入口生命周期管理，不回收）
        if (entranceVfxPrefab != null)
            VFXPool.Spawn(entranceVfxPrefab, transform.position, Quaternion.identity, transform, 0f);
    }

    private void Update()
    {
        if (!_playerInside) return;

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            EnterBattle();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;

        _playerInside = true;
        Debug.Log($"[BossEntrance] 玩家进入 Boss 房入口范围，按 F 键进入战斗（提示预制体未就绪，先用日志代替）");
        ShowPrompt(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == null) return;

        _playerInside = false;
        Debug.Log($"[BossEntrance] 玩家离开 Boss 房入口范围：{other.name}");
        ShowPrompt(false);
    }

    private void ShowPrompt(bool show)
    {
        if (promptText == null) return;
        if (show) promptText.text = promptMessage;
        promptText.gameObject.SetActive(show);
    }

    private void EnterBattle()
    {
        _playerInside = false;
        ShowPrompt(false);
        Debug.Log($"[BossEntrance] 按 F 确认，进入战斗场景：{battleSceneName}");
        SceneLoader.Instance.LoadScene(battleSceneName);
    }
}
