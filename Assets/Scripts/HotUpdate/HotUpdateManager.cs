using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 热更管理器（M14）— 版本检查 → 下载 AB → 切换 provider → 预加载关键资源。
/// BootFlow 调 RunFlow 并把真实进度喂给 LoadingFlow。
/// 完成时发 HotUpdateCompleted（BGM 等早期加载方监听后重新从 AB 拉取）。
///
/// 兜底策略：
///   编辑器内未构建/未开启 AB → EditorAssetProvider（直接从 Assets/GameAssets 加载，开发不断流）；
///   正式包必须构建 AB（CDN version.txt 存在才走 AB，否则保持 Resources 兜底并报错提示）。
/// </summary>
public class HotUpdateManager : GameModule<HotUpdateManager>
{
    /// <summary>filelist.json 单文件条目（构建工具生成，运行时增量下载依据）</summary>
    [Serializable]
    public class FileEntry
    {
        public string name;   // 相对 CDN 根的文件路径（/ 分隔）
        public string hash;   // 文件 MD5（小写 hex）
        public long size;     // 文件字节数
    }

    /// <summary>filelist.json 根结构</summary>
    [Serializable]
    public class FileListData
    {
        public string version = "";
        public string rootBundle = "";   // manifest bundle 文件名（Unity 约定 = 构建输出目录名，如 Windows）
        public List<FileEntry> files = new();
    }

    [Header("热更开关")]
    [Tooltip("是否启用 AssetBundle 热更；关掉后走开发兜底加载")]
    [SerializeField] private bool enableAssetBundle = true;

    [Header("模拟 CDN（本地文件夹）")]
    [Tooltip("CDN 根目录（相对项目根即可，如 HotUpdateCDN；也支持绝对路径），存放 version.txt/bundlemap.json/各 bundle")]
    [SerializeField] private string cdnRoot = "HotUpdateCDN";

    [Header("本地缓存")]
    [Tooltip("AB 下载缓存目录（相对 Application.persistentDataPath）")]
    [SerializeField] private string localBundleRoot = "HotUpdate/AB";

    private bool _flowStarted;
    private bool _flowDone;
    private bool _abReady;
    private string _manifestFileName = "AssetBundleManifest";

    /// <summary>本次启动是否执行过热更流程（BgmManager 等早期模块据此决定是否等待流程结束）</summary>
    public static bool FlowWillRun { get; private set; }

    /// <summary>状态文案回调（BootFlow 订阅，转发给 LoadingFlow 的状态 TMP）</summary>
    public event Action<string> StatusChanged;

    public bool IsFlowDone => _flowDone;
    public bool IsAbReady => _abReady;

    /// <summary>热更流程中预加载的关键资源（地址，走 provider 加载进缓存）</summary>
    private static readonly string[] PreloadAddresses =
    {
        "Prefabs/安比",
        "Prefabs/Bangboo",
        "Prefabs/怪兽",
        "UI/Panels/GameLaunchView",
        "UI/Panels/GamePanel",
        "UI/Panels/TeamUpView",
        "UI/Panels/AddView",
        "UI/Panels/WinView",
        "UI/Panels/LoadingBar",
        "UI/Panels/LoadingPanelMain",
        "UI/Panels/LoadingPanelSixthStreet",
        "BGM/World",
        "BGM/Battle",
    };

    protected override void OnInit()
    {
        Debug.Log("[HotUpdateManager] 初始化完成");
    }

    /// <summary>真实热更流程（幂等）：版本检查 → 下载 → provider 就绪 → 预加载 → HotUpdateCompleted</summary>
    public async UniTask RunFlow(IProgress<float> progress = null)
    {
        if (_flowDone)
        {
            progress?.Report(1f);
            return;
        }
        if (_flowStarted)
        {
            await UniTask.WaitUntil(() => _flowDone);
            progress?.Report(1f);
            return;
        }
        _flowStarted = true;
        FlowWillRun = true;

        SetStatus("正在进入游戏");
        Report(progress, 0.02f);

        if (enableAssetBundle && TryResolveSourcePath(out var source))
        {
            Debug.Log($"[HotUpdateManager] 资源源：{source}");
            Report(progress, 0.05f);   // 版本检查
            var downloaded = await DownloadIfNeeded(source, progress);   // 0.05 → 0.5
            if (downloaded)
            {
                Report(progress, 0.55f);
                var abProvider = new AssetBundleAssetProvider();
                if (abProvider.Initialize(LocalBundleFullPath(), _manifestFileName))
                {
                    ResourceManager.Instance.SetProvider(abProvider);
                    _abReady = true;
                    await PreloadKeyAssets(progress);   // 0.55 → 0.9
                }
                else
                {
                    abProvider.Reset();
                    Debug.LogWarning("[HotUpdateManager] AB 初始化失败，切换兜底加载");
                }
            }
            else
            {
                Debug.LogWarning("[HotUpdateManager] 未检测到可用的 AB 版本（CDN 缺失或下载失败），走兜底加载");
            }
        }

        if (!_abReady)
            ApplyFallbackProvider();

        Report(progress, 1f);
        _flowDone = true;
        SetStatus("正在进入游戏");
        EventBus.Emit(GameEvents.HotUpdateCompleted);
        Debug.Log($"[HotUpdateManager] 热更流程完成，资源来源：{(IsAbReady ? $"AssetBundle({LocalBundleFullPath()})" : "兜底")}");
    }

    /// <summary>
    /// 版本检查 + 增量下载（逐文件 MD5 对比，只拉缺失/变更的文件）；
    /// CDN 缺 filelist.json 时降级为全量拷贝。返回本地 AB 是否可用。
    /// </summary>
    private async UniTask<bool> DownloadIfNeeded(string cdn, IProgress<float> progress)
    {
        var remoteVersionPath = Path.Combine(cdn, "version.txt");
        if (!File.Exists(remoteVersionPath))
        {
            Debug.LogWarning($"[HotUpdateManager] CDN 缺少 version.txt：{cdn}");
            return false;
        }

        var remoteVersion = File.ReadAllText(remoteVersionPath).Trim();
        var localRoot = LocalBundleFullPath();
        var localVersionPath = Path.Combine(localRoot, "version.txt");

        var upToDate = File.Exists(localVersionPath)
                       && File.ReadAllText(localVersionPath).Trim() == remoteVersion
                       && File.Exists(Path.Combine(localRoot, "filelist.json"))
                       && File.Exists(Path.Combine(localRoot, ReadLocalRootBundleName(localRoot)));
        if (upToDate)
        {
            // fast-path 跳过下载时也要拿到 manifest 文件名（本地 filelist 里有 rootBundle）
            _manifestFileName = ReadLocalRootBundleName(localRoot);
            Debug.Log($"[HotUpdateManager] 本地已是最新版本 {remoteVersion}，跳过下载");
            return true;
        }

        Debug.Log($"[HotUpdateManager] 发现新版本 {remoteVersion}（本地 {ReadLocalVersion(localVersionPath)}），开始下载 AB");
        SetStatus("检测到新版本，资源更新中");
        Report(progress, 0.1f);

        // 先取远端清单（增量依据）；拿不到就降级全量拷贝
        var remoteFileList = ReadFileList(Path.Combine(cdn, "filelist.json"));
        if (remoteFileList == null)
        {
            Debug.LogWarning("[HotUpdateManager] CDN 缺少 filelist.json，降级为全量拷贝");
            await CopyAllFiles(cdn, localRoot, progress);
            File.Copy(Path.Combine(cdn, "filelist.json"), Path.Combine(localRoot, "filelist.json"), true);
            File.WriteAllText(localVersionPath, remoteVersion);
            return true;
        }

        // manifest bundle 文件名以 CDN 清单为准（Unity 输出目录名，如 Windows）
        _manifestFileName = string.IsNullOrEmpty(remoteFileList.rootBundle)
            ? "AssetBundleManifest"
            : remoteFileList.rootBundle;

        // 把最新清单同步到本地（本地 fast-path 判断依赖它；filelist 不在增量文件列表里，需单独拷贝）
        var destFileList = Path.Combine(localRoot, "filelist.json");
        var destFileListDir = Path.GetDirectoryName(destFileList);
        if (!string.IsNullOrEmpty(destFileListDir)) Directory.CreateDirectory(destFileListDir);
        File.Copy(Path.Combine(cdn, "filelist.json"), destFileList, true);

        // 逐文件对比本地 MD5：一致跳过，不一致/缺失才下载
        long total = 0, done = 0;
        var changed = 0;
        foreach (var entry in remoteFileList.files)
        {
            total += entry.size;
        }
        if (total <= 0)
        {
            Debug.LogWarning("[HotUpdateManager] filelist.json 为空，无可下载文件");
            File.WriteAllText(localVersionPath, remoteVersion);
            return true;
        }

        foreach (var entry in remoteFileList.files)
        {
            var dest = Path.Combine(localRoot, entry.name);
            if (Md5File(dest) == entry.hash)
            {
                done += entry.size;   // 已是最新，按已完成计，保证进度单调
                continue;
            }

            var src = Path.Combine(cdn, entry.name);
            if (!File.Exists(src))
            {
                Debug.LogWarning($"[HotUpdateManager] CDN 缺文件：{entry.name}，跳过");
                continue;
            }

            var dir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Copy(src, dest, true);
            changed++;
            done += entry.size;
            Report(progress, 0.1f + 0.4f * (float)(done / (double)total));
            await UniTask.Yield();
        }

        File.WriteAllText(localVersionPath, remoteVersion);
        Debug.Log($"[HotUpdateManager] 增量更新完成：{changed} 个文件变更 → {localRoot}");
        return true;
    }

    /// <summary>全量拷贝 CDN → 本地（filelist.json 缺失时的降级路径）</summary>
    private async UniTask CopyAllFiles(string cdn, string localRoot, IProgress<float> progress)
    {
        var files = new List<(string src, string rel, long size)>();
        foreach (var file in Directory.GetFiles(cdn, "*", SearchOption.AllDirectories))
        {
            var rel = file.Substring(cdn.Length).TrimStart('\\', '/');
            files.Add((file, rel, new FileInfo(file).Length));
        }

        long total = 0;
        foreach (var f in files) total += f.size;
        if (total <= 0)
        {
            Debug.LogError("[HotUpdateManager] CDN 目录为空，无可下载文件");
            return;
        }

        long done = 0;
        foreach (var (src, rel, size) in files)
        {
            var dest = Path.Combine(localRoot, rel);
            var dir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Copy(src, dest, true);
            done += size;
            Report(progress, 0.1f + 0.4f * (float)(done / (double)total));
            await UniTask.Yield();
        }

        Debug.Log($"[HotUpdateManager] 全量拷贝完成：{files.Count} 个文件 / {total} 字节 → {localRoot}");
    }

    private static FileListData ReadFileList(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonUtility.FromJson<FileListData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[HotUpdateManager] 解析 filelist.json 失败：{e.Message}");
            return null;
        }
    }

    /// <summary>读本地 filelist.json 的 manifest bundle 文件名（缺省按 AssetBundleManifest 兜底）</summary>
    private static string ReadLocalRootBundleName(string localRoot)
    {
        var localFileList = ReadFileList(Path.Combine(localRoot, "filelist.json"));
        return localFileList != null && !string.IsNullOrEmpty(localFileList.rootBundle)
            ? localFileList.rootBundle
            : "AssetBundleManifest";
    }

    private static string Md5File(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var stream = File.OpenRead(path);
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[HotUpdateManager] MD5 计算失败 {path}：{e.Message}");
            return null;
        }
    }

    /// <summary>预加载关键资源（进 ResourceManager 缓存），逐项喂进度</summary>
    private async UniTask PreloadKeyAssets(IProgress<float> progress)
    {
        for (var i = 0; i < PreloadAddresses.Length; i++)
        {
            var address = PreloadAddresses[i];
            var asset = await ResourceManager.Instance.LoadAsync<UnityEngine.Object>(address);
            if (asset == null)
                Debug.LogWarning($"[HotUpdateManager] 预加载失败（可能未打进包）：{address}");
            Report(progress, 0.55f + 0.35f * ((i + 1f) / PreloadAddresses.Length));
        }
    }

    private void ApplyFallbackProvider()
    {
#if UNITY_EDITOR
        ResourceManager.Instance.SetProvider(new EditorAssetProvider());
        Debug.Log("[HotUpdateManager] 使用编辑器兜底加载（Assets/GameAssets 直读）");
#else
        ResourceManager.Instance.SetProvider(new ResourcesAssetProvider());
        Debug.LogWarning("[HotUpdateManager] 正式包未走 AB：请先构建并发布 AssetBundle（菜单：热更/一键构建并发布）");
#endif
    }

    /// <summary>
    /// 解析资源源：优先外部 CDN（本地文件夹模拟，可热更）；没有就退回内置首包（StreamingAssets，随主包）。
    /// </summary>
    private bool TryResolveSourcePath(out string path)
    {
        // 1) 外部 CDN（默认相对项目根 / exe 目录的 HotUpdateCDN）
        var external = Path.IsPathRooted(cdnRoot)
            ? cdnRoot
            : Path.Combine(Application.dataPath, "..", cdnRoot);
        external = Path.GetFullPath(external);
        if (Directory.Exists(external))
        {
            path = external;
            return true;
        }

        // 2) 内置首包（StreamingAssets/HotUpdate/AB，随主包发布）
        var builtIn = Path.Combine(Application.streamingAssetsPath, "HotUpdate", "AB");
        if (Directory.Exists(builtIn) && File.Exists(Path.Combine(builtIn, "filelist.json")))
        {
            path = builtIn;
            return true;
        }

        path = null;
        return false;
    }

    private string LocalBundleFullPath() => Path.Combine(Application.persistentDataPath, localBundleRoot);

    private static string ReadLocalVersion(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : "(无)";
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[HotUpdateManager] 读本地版本失败：{e.Message}");
            return "(无)";
        }
    }

    private static void Report(IProgress<float> progress, float value)
    {
        progress?.Report(Mathf.Clamp01(value));
    }

    private void SetStatus(string status)
    {
        StatusChanged?.Invoke(status);
    }
}
