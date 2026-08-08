using System;

/// <summary>
/// 日志抽象 — 同一份 Net 代码既能跑在 Unity 客户端，也能编译成纯控制台服务器：
/// Unity 环境走 Debug.Log，独立进程走 Console.WriteLine。
/// </summary>
public static class NetLog
{
    public static void Log(string msg)
    {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS
        UnityEngine.Debug.Log(msg);
#else
        Console.WriteLine($"[NET] {msg}");
#endif
    }

    public static void Warn(string msg)
    {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS
        UnityEngine.Debug.LogWarning(msg);
#else
        Console.WriteLine($"[NET][WARN] {msg}");
#endif
    }

    public static void Error(string msg)
    {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_ANDROID || UNITY_IOS
        UnityEngine.Debug.LogError(msg);
#else
        Console.WriteLine($"[NET][ERROR] {msg}");
#endif
    }
}
