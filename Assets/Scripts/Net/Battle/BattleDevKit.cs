using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;
using Cysharp.Threading.Tasks;

/// <summary>
/// 战斗测试开发面板（OnGUI）— Main 场景进 Play 后按 F9 呼出，不改任何场景文件。
///
/// 为什么用 OnGUI：这是开发工具界面，不需要 Canvas/预制体资产；正式版会被大厅进房流程替代。
/// 职责：手动扮演"大厅"的角色——决定谁是房主（起 7778）、谁连接谁。
///
/// 单实例快速闭环测试：先点"作为主机"，再把名字改成 Guest 点"连接主机"，
/// 同一进程内服务器 + 客户端跑通：你控制的角色 → 快照 → 幽灵跟随（闭环可见）。
/// 双实例真实联网：实例 A 当主机，实例 B（打包 exe）连 A 的 IP。
/// </summary>
public class BattleDevKit : MonoBehaviour
{
    private string _ip = "127.0.0.1";
    private string _name = "Host";
    private bool _showPanel;
    private BattleHostRuntime _host;
    private BattleClientRuntime _client;
    private float _perfLogTimer;   // [Perf] 性能日志节流（FPS + 内存）

    /// <summary>Main 场景加载后自动创建本面板（零场景改动：代码启动开发工具）</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        // 兜底：直接从 Main 场景启动（打包/编辑器直接 Play Main）时，Boot 的 GameEntry 没跑，
        // 常驻模块（含 ESC 呼出鼠标的 CursorEscapeToggle）未初始化，这里补齐。
        // GameModules.Init() 内部有防重守卫：从 Boot 正常启动时重复调用无害。
        GameModules.Init();
        // 直进 Main（无 GameEntry）：兜底驱动资源引导，切 AB 后 HUD 才可用。
        // 只在打包运行时执行——编辑器保持 EditorAssetProvider 直读项目资源，
        // 否则改 prefab 后编辑器也会从旧 AB 加载（看不到改动）。
#if !UNITY_EDITOR
        HotUpdateManager.Instance.RunFlow().Forget();
#endif

        // 注意：RuntimeInitializeOnLoadMethod(AfterSceneLoad) 只在"启动时第一个场景加载后"执行一次，
        // 场景切换不会再次触发。所以本面板必须常驻跨场景，用 sceneLoaded 监听 Main 加载。
        var go = new GameObject("BattleDevKit");
        DontDestroyOnLoad(go);
        go.AddComponent<BattleDevKit>();
        Debug.Log($"[BattleFlow] BattleDevKit 常驻创建，启动场景={SceneManager.GetActiveScene().name}");
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Debug.Log($"[BattleFlow] BattleDevKit Awake，场景={SceneManager.GetActiveScene().name}");
        ApplyForScene(SceneManager.GetActiveScene().name);   // 启动场景就是 Main 时也生效
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyForScene(scene.name);
    }

    /// <summary>
    /// 进入六分街（游历家园联机）：从大厅流程则自动起服务器/连客户端，不用手动点面板。
    /// Main（战斗场景）是单人进入，不自动启动联机。
    /// 由场景加载事件驱动（面板常驻跨场景），不是启动时的一次性判断。
    /// </summary>
    private void ApplyForScene(string sceneName)
    {
        if (sceneName != "SixthStreet") return;
        Debug.Log($"[BattleFlow] 进入六分街：FromLobby={BattleSessionState.FromLobby} IsHost={BattleSessionState.IsHost}");
        if (!BattleSessionState.FromLobby) return;

        if (BattleSessionState.IsHost)
            AutoStartHost();
        else
            AutoConnectClient();
    }

    private void Start()
    {
        // 默认名字：有大厅注册名用大厅名，否则 "Host"（双实例时记得手动改客户端名，避免重名被拒）
        if (LobbyClientService.HasInstance && !string.IsNullOrEmpty(LobbyClientService.Instance.MyName))
            _name = LobbyClientService.Instance.MyName;
    }

    private void Update()
    {
        // [Perf] 性能日志：每秒打一次 FPS + 总分配内存（查卡顿/GC 分配用）
        _perfLogTimer -= Time.unscaledDeltaTime;
        if (_perfLogTimer <= 0f)
        {
            _perfLogTimer = 1f;
            float fps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;
            float mb = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f);
            Debug.Log($"[Perf] FPS={fps:F0} 总分配内存={mb:F1}MB");
        }

        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
        {
            _showPanel = !_showPanel;
            // 面板打开 = 调试状态：禁用角色输入（防点击面板按钮触发攻击/闪避）+ 显示鼠标；
            // 关闭时恢复输入 + 隐藏鼠标。复用 PlayerInputGate（与组队界面同一套机制）。
            if (_showPanel) PlayerInputGate.EnterUI();
            else PlayerInputGate.ExitUI();
        }
    }

    private void OnGUI()
    {
        if (!_showPanel) return;

        GUILayout.BeginArea(new Rect(16, 16, 340, 300), GUI.skin.box);
        GUILayout.Label("【战斗测试面板】 F9 开关");
        GUILayout.Label($"服务器：{(_host != null ? "已启动" : "未启动")}");
        GUILayout.Label($"客户端：{(_client != null ? (_client.Connected ? "已连接" : "连接中/失败") : "未连接")}");
        if (_client != null)
            GUILayout.Label($"我的名字：{_client.MyName}  幽灵数：{_client.GhostCount}");
        GUILayout.Space(8);

        GUILayout.Label("房主 IP：");
        _ip = GUILayout.TextField(_ip);
        GUILayout.Label("我的名字（双开时两个实例不能重名）：");
        _name = GUILayout.TextField(_name);
        GUILayout.Space(8);

        if (GUILayout.Button("作为主机启动战斗（7778）") && _host == null)
        {
            var go = new GameObject("BattleHostRuntime");
            _host = go.AddComponent<BattleHostRuntime>();
            _host.SetHostName(_name);   // Start 之前指定房主名
        }

        if (GUILayout.Button("连接主机") && _client == null)
        {
            var go = new GameObject("BattleClientRuntime");
            _client = go.AddComponent<BattleClientRuntime>();
            _client.SetHideLocalBoss(_host == null);   // 单进程同时有主机时不藏 Boss（主机管）
            _client.SetReconcileLocal(_host == null);  // 单进程时本地玩家=房主，没有独立客户端模拟，纠偏会拉飞
            _client.SetSingleProcessDemo(_host != null); // 单进程：不开幽灵/伤害应用（避免鬼影和血量串台）
            _client.Configure(_ip, 7778, _name);
        }

        if (GUILayout.Button("停止战斗（停服/断开）"))
        {
            if (_client != null) { Destroy(_client.gameObject); _client = null; }
            if (_host != null) { Destroy(_host.gameObject); _host = null; }
        }
        GUILayout.EndArea();
    }

    /// <summary>大厅流程自动启动：房主 → 起 BattleServer（成员/槽位来自 BattleSessionState）</summary>
    public void AutoStartHost()
    {
        if (_host != null) return;
        var go = new GameObject("BattleHostRuntime");
        _host = go.AddComponent<BattleHostRuntime>();
        _host.SetHostName(BattleSessionState.HostName);
        Debug.Log("[BattleFlow] 自动启动主机战斗服务器");
    }

    /// <summary>大厅流程自动连接：客人 → 连房主（地址/名字来自 BattleSessionState）</summary>
    public void AutoConnectClient()
    {
        if (_client != null) return;
        var go = new GameObject("BattleClientRuntime");
        _client = go.AddComponent<BattleClientRuntime>();
        _client.SetHideLocalBoss(true);
        _client.SetReconcileLocal(true);
        _client.SetSingleProcessDemo(false);
        _client.Configure(BattleSessionState.HostIp, BattleSessionState.HostPort, BattleSessionState.MyName);
        Debug.Log("[BattleFlow] 自动连接房主战斗服务器");
    }
}
