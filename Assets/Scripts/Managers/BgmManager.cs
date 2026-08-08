using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 背景音乐管理器 — 常驻单例
/// Boot 与 SixthStreet 共用一个 clip，场景切换不打断；Main 单独一个 clip。
/// AudioSource 挂在 DontDestroyOnLoad 的模块物体上，跨场景持续播放。
/// 音频走资源提供者（AB/编辑器兜底）。
/// BGM 不在一启动就播：先等热更流程结束（provider 已定），再按最终来源加载播放，
/// 避免"先播本地新音乐、随后被 AB 旧音乐替换"的跳变；直接 Play 非启动场景则立即加载。
/// </summary>
public class BgmManager : GameModule<BgmManager>
{
    [Header("BGM 资源地址（Assets/GameAssets/BGM 下）")]
    [Tooltip("Boot + 六分街共用")]
    [SerializeField] private string worldBgmPath = "BGM/World";
    [Tooltip("Main 战斗场景")]
    [SerializeField] private string battleBgmPath = "BGM/Battle";

    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.5f;

    private AudioSource _source;
    private AudioClip _worldClip;
    private AudioClip _battleClip;
    private bool _clipsLoading;

    protected override void Awake()
    {
        base.Awake();
        if (BgmManager.Instance != this) return;   // 重复实例被销毁，不再初始化
        InitAudio();
    }

    protected override void OnInit()
    {
        // 音频在 Awake 里就绪，支持直接从非启动场景 Play（不依赖 GameModules.Init 时序）
        Debug.Log("[BgmManager] 初始化完成");
    }

    private void InitAudio()
    {
        if (_source != null) return;   // 幂等

        _source = gameObject.AddComponent<AudioSource>();
        _source.loop = true;
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;     // 2D 全局音乐
        _source.volume = volume;

        SceneManager.sceneLoaded += OnSceneLoaded;
        WaitForHotUpdateThenLoad();
    }

    /// <summary>
    /// 等热更流程结束后再从最终 provider 加载 BGM：
    /// 正常 Boot → 流程结束（IsFlowDone）后加载；直接 Play 无流程 → 立即加载。
    /// </summary>
    private async void WaitForHotUpdateThenLoad()
    {
        // 等一帧，让 BootFlow.Start 有机会启动热更流程（Awake 先于 Start 执行）
        await UniTask.DelayFrame(1);

        if (!HotUpdateManager.FlowWillRun)
        {
            LoadClipsFromProvider();   // 直接 Play 非启动场景 / 场景里没有 BootFlow
            return;
        }

        // 流程正常都会结束（含兜底）；30s 超时兜底，防流程异常卡死导致全程没音乐
        await UniTask.WhenAny(
            UniTask.WaitUntil(() => HotUpdateManager.Instance.IsFlowDone),
            UniTask.Delay(30000)
        );
        LoadClipsFromProvider();
    }

    private async void LoadClipsFromProvider()
    {
        if (_clipsLoading) return;
        _clipsLoading = true;

        var world = await ResourceManager.Instance.LoadFreshAsync<AudioClip>(worldBgmPath);
        var battle = await ResourceManager.Instance.LoadFreshAsync<AudioClip>(battleBgmPath);

        if (world != null) _worldClip = world;
        if (battle != null) _battleClip = battle;
        if (world == null) Debug.LogWarning($"[BgmManager] 未找到 {worldBgmPath}（Assets/GameAssets/BGM/）");
        if (battle == null) Debug.LogWarning($"[BgmManager] 未找到 {battleBgmPath}（Assets/GameAssets/BGM/）");

        _clipsLoading = false;
        ApplySceneMusic(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneMusic(scene.name);
    }

    /// <summary>按场景切音乐：Main 用战斗曲，其余（Boot/六分街）用世界曲；同一段不打断</summary>
    private void ApplySceneMusic(string sceneName)
    {
        var target = sceneName == "Main" ? _battleClip : _worldClip;
        if (target == null) return;

        if (_source.clip == target && _source.isPlaying) return;   // 同段音乐继续放，不重启

        _source.clip = target;
        _source.Play();
    }

    protected override void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        base.OnDestroy();
    }
}
