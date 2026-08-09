using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

/// <summary>
/// AssetBundle 构建工具（热更 · M13）
///
/// 菜单：
///   热更/1. 分配 AB 包名      —— 按分包规则给资源打 bundle 名
///   热更/2. 构建 AssetBundle  —— 构建到 Build/AssetBundles/Windows
///   热更/3. 发布到模拟 CDN    —— 拷贝到项目根 HotUpdateCDN，并写 version.txt + bundlemap.json
///   热更/一键构建并发布       —— 2 + 3
///
/// 改资源后：把 Version 加一 → 一键构建并发布 → 客户端启动时自动拉新。
/// 分包：角色 / 敌人 / UI / 音频（场景留 Build Settings 不进 AB，热更包瘦身，主包大点无所谓）。
/// 注意：托管资源在 Assets/GameAssets（已迁出 Resources），依赖资源（模型/材质/动画等）
/// 未单独分包时会被 Unity 自动收进引用它的 bundle。
/// </summary>
public static class AssetBundleBuilder
{
    public const string Version = "1.0.8";   // 资源有改动就 +1，客户端据此判断是否需要热更
    public const string OutputRelative = "Build/AssetBundles/Windows";
    public const string CdnRelative = "HotUpdateCDN";
    public const string StreamingRelative = "Assets/StreamingAssets/HotUpdate/AB";   // 内置首包

    private static string OutputPath => Path.Combine(Application.dataPath, "..", OutputRelative);
    private static string CdnPath => Path.Combine(Application.dataPath, "..", CdnRelative);
    private static string StreamingPath => Path.Combine(Application.dataPath, StreamingRelative);

    /// <summary>分包规则：bundle 名 → 资源路径（单文件或目录；目录下所有资产进同一包）</summary>
    private static readonly (string bundle, string path)[] BundleRules =
    {
        ("character/player",  "Assets/GameAssets/Prefabs/安比.prefab"),
        ("character/pet",     "Assets/GameAssets/Prefabs/Bangboo.prefab"),
        ("enemy/monster",     "Assets/GameAssets/Prefabs/怪兽.prefab"),
        ("ui/panels",         "Assets/GameAssets/UI/Panels"),
        ("audio/bgm",         "Assets/GameAssets/BGM"),
    };

    [MenuItem("热更/1. 分配 AB 包名")]
    public static void AssignBundleNames()
    {
        // 先清掉管理目录里所有已设置的 bundle 名（防止"从规则里删掉的资源"还挂着旧包名被继续打包）
        ClearManagedBundleNames();

        var count = 0;
        foreach (var (bundle, path) in BundleRules)
        {
            foreach (var assetPath in EnumerateAssets(path))
            {
                var importer = AssetImporter.GetAtPath(assetPath);
                if (importer == null) continue;
                importer.SetAssetBundleNameAndVariant(bundle, "");
                count++;
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[AssetBundleBuilder] 分配完成：{count} 个资源");
    }

    /// <summary>
    /// 清空 Assets/Scene 与 Assets/GameAssets 下所有资源的 bundle 名，
    /// 然后 AssignBundleNames 再按当前 BundleRules 重新分配。
    /// </summary>
    private static void ClearManagedBundleNames()
    {
        var cleared = 0;
        foreach (var root in new[] { "Assets/Scene", "Assets/GameAssets" })
        {
            foreach (var assetPath in EnumerateAssets(root))
            {
                var importer = AssetImporter.GetAtPath(assetPath);
                if (importer == null) continue;
                if (string.IsNullOrEmpty(importer.assetBundleName)) continue;
                importer.SetAssetBundleNameAndVariant("", "");
                cleared++;
            }
        }
        if (cleared > 0)
            Debug.Log($"[AssetBundleBuilder] 已清除 {cleared} 个旧 bundle 名");
    }

    [MenuItem("热更/2. 构建 AssetBundle")]
    public static void BuildBundles()
    {
        AssignBundleNames();
        EnsureUnderProject(OutputPath);
        if (Directory.Exists(OutputPath)) Directory.Delete(OutputPath, true);
        Directory.CreateDirectory(OutputPath);

        var report = BuildPipeline.BuildAssetBundles(
            OutputPath,
            BuildAssetBundleOptions.ChunkBasedCompression,
            BuildTarget.StandaloneWindows64);

        if (report == null)
        {
            Debug.LogError("[AssetBundleBuilder] 构建失败，请查看 Console 报错");
            return;
        }

        var bundleNames = report.GetAllAssetBundles();
        if (bundleNames == null || bundleNames.Length == 0)
        {
            Debug.LogError("[AssetBundleBuilder] 构建产物为空，请检查分包规则");
            return;
        }

        WriteBundlemap();
        WriteFileList();
        Debug.Log($"[AssetBundleBuilder] 构建完成：{bundleNames.Length} 个 bundle → {OutputPath}");
    }

    [MenuItem("热更/3. 发布到模拟 CDN")]
    public static void PublishToCdn()
    {
        if (!Directory.Exists(OutputPath))
        {
            Debug.LogError("[AssetBundleBuilder] 请先执行：热更/2. 构建 AssetBundle");
            return;
        }

        EnsureUnderProject(CdnPath);
        if (Directory.Exists(CdnPath)) Directory.Delete(CdnPath, true);
        Directory.CreateDirectory(CdnPath);

        CopyDirectory(OutputPath, CdnPath);
        File.WriteAllText(Path.Combine(CdnPath, "version.txt"), Version);
        if (File.Exists(Path.Combine(OutputPath, "bundlemap.json")))
            File.Copy(Path.Combine(OutputPath, "bundlemap.json"), Path.Combine(CdnPath, "bundlemap.json"), true);

        Debug.Log($"[AssetBundleBuilder] 已发布到模拟 CDN：{CdnPath}（version={Version}）");
    }

    [MenuItem("热更/4. 生成内置首包（StreamingAssets）")]
    public static void CopyToStreamingAssets()
    {
        if (!Directory.Exists(OutputPath))
        {
            Debug.LogError("[AssetBundleBuilder] 请先执行：热更/2. 构建 AssetBundle");
            return;
        }

        EnsureUnderProject(StreamingPath);
        if (Directory.Exists(StreamingPath)) Directory.Delete(StreamingPath, true);
        Directory.CreateDirectory(StreamingPath);

        CopyDirectory(OutputPath, StreamingPath);
        File.WriteAllText(Path.Combine(StreamingPath, "version.txt"), Version);
        Debug.Log($"[AssetBundleBuilder] 内置首包已生成：{StreamingPath}（随主包发布，无网可玩）");
    }

    [MenuItem("热更/一键构建并发布")]
    public static void BuildAndPublish()
    {
        BuildBundles();
        PublishToCdn();
    }

    /// <summary>遍历规则路径：目录 → 目录下所有资产；单文件 → 它自己</summary>
    private static IEnumerable<string> EnumerateAssets(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return AssetDatabase.FindAssets("", new[] { path })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !AssetDatabase.IsValidFolder(p));
        }
        return new[] { path };
    }

    /// <summary>生成 bundlemap.json（地址 → bundle + 资源全路径），运行时 AssetBundleAssetProvider 读取</summary>
    private static void WriteBundlemap()
    {
        var data = new AssetBundleAssetProvider.BundlemapData();
        foreach (var (bundle, path) in BundleRules)
        {
            foreach (var assetPath in EnumerateAssets(path))
            {
                data.entries.Add(new AssetBundleAssetProvider.BundlemapEntry
                {
                    address = ToAddress(assetPath, bundle),
                    assetPath = assetPath.ToLowerInvariant(),
                    bundle = bundle,
                });
            }
        }
        File.WriteAllText(Path.Combine(OutputPath, "bundlemap.json"), JsonUtility.ToJson(data, true));
        Debug.Log($"[AssetBundleBuilder] bundlemap.json 已生成：{data.entries.Count} 条");
    }

    /// <summary>
    /// 生成 filelist.json（每个文件相对路径 + MD5 + 大小），运行时按它做增量下载：
    /// 只拉本地缺失或 hash 不一致的文件。必须最后生成（把 bundlemap.json 也纳入清单）。
    /// </summary>
    private static void WriteFileList()
    {
        var data = new HotUpdateManager.FileListData
        {
            version = Version,
            rootBundle = new DirectoryInfo(OutputPath).Name,   // Unity 约定：manifest bundle 文件 = 输出目录名
        };
        foreach (var file in Directory.GetFiles(OutputPath, "*", SearchOption.AllDirectories))
        {
            var rel = file.Substring(OutputPath.Length).TrimStart('\\', '/').Replace('\\', '/');
            data.files.Add(new HotUpdateManager.FileEntry
            {
                name = rel,
                hash = Md5File(file),
                size = new FileInfo(file).Length,
            });
        }

        File.WriteAllText(Path.Combine(OutputPath, "filelist.json"), JsonUtility.ToJson(data, true));
        Debug.Log($"[AssetBundleBuilder] filelist.json 已生成：{data.files.Count} 个文件");
    }

    private static string Md5File(string path)
    {
        using var stream = File.OpenRead(path);
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>资源全路径 → 运行时地址：场景用文件名（main/sixthstreet），其余去 Assets/GameAssets/ 前缀与扩展名</summary>
    private static string ToAddress(string assetPath, string bundle)
    {
        if (bundle.StartsWith("scene/", StringComparison.Ordinal))
            return Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();

        const string prefix = "Assets/GameAssets/";
        if (assetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var rel = assetPath.Substring(prefix.Length);
            rel = Path.ChangeExtension(rel, null);
            return rel.Replace('\\', '/').ToLowerInvariant();
        }

        return assetPath.Replace('\\', '/').ToLowerInvariant();
    }

    /// <summary>整目录拷贝（含子目录与文件）</summary>
    private static void CopyDirectory(string src, string dest)
    {
        foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
        {
            var rel = dir.Substring(src.Length).TrimStart('\\', '/');
            Directory.CreateDirectory(Path.Combine(dest, rel));
        }
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var rel = file.Substring(src.Length).TrimStart('\\', '/');
            File.Copy(file, Path.Combine(dest, rel), true);
        }
    }

    /// <summary>安全校验：构建/发布目录必须落在项目根内，防止误删项目外目录</summary>
    private static void EnsureUnderProject(string path)
    {
        var full = Path.GetFullPath(path);
        var project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        if (!full.StartsWith(project, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"[AssetBundleBuilder] 目标路径必须在项目内：{full}");
    }
}
