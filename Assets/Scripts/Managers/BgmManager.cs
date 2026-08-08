using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 背景音乐管理器 — 常驻单例
/// Boot 与 SixthStreet 共用一个 clip，场景切换不打断；Main 单独一个 clip。
/// AudioSource 挂在 DontDestroyOnLoad 的模块物体上，跨场景持续播放。
/// </summary>
public class BgmManager : GameModule<BgmManager>
{
    [Header("BGM 资源路径（Assets/Resources/BGM 下）")]
    [Tooltip("Boot + 六分街共用")]
    [SerializeField] private string worldBgmPath = "BGM/World";
    [Tooltip("Main 战斗场景")]
    [SerializeField] private string battleBgmPath = "BGM/Battle";

    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.5f;

    private AudioSource _source;
    private AudioClip _worldClip;
    private AudioClip _battleClip;

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

        _worldClip = Resources.Load<AudioClip>(worldBgmPath);
        _battleClip = Resources.Load<AudioClip>(battleBgmPath);

        if (_worldClip == null) Debug.LogWarning($"[BgmManager] 未找到 {worldBgmPath}，请放到 Assets/Resources/BGM/");
        if (_battleClip == null) Debug.LogWarning($"[BgmManager] 未找到 {battleBgmPath}，请放到 Assets/Resources/BGM/");

        SceneManager.sceneLoaded += OnSceneLoaded;

        // 启动场景（Boot）也立即播放
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
