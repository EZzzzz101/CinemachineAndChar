using System;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// 解析 LLM 返回的 JSON，带正则容错
/// </summary>
public static class ResponseParser
{
    [Serializable]
    private class LLMResponse
    {
        public string type;
        public string content;
        public string reply;  // action 类型的口语回复
    }

    /// <summary>
    /// 尝试从 LLM 原始输出中提取 type、content、reply
    /// </summary>
    /// <returns>true 表示解析成功</returns>
    public static bool TryParse(string rawJson, out string type, out string content, out string reply)
    {
        type    = null;
        content = null;
        reply   = null;

        if (string.IsNullOrWhiteSpace(rawJson)) return false;

        rawJson = rawJson.Trim();

        // 主路径：直接 JSON 解析
        try
        {
            var resp = JsonUtility.FromJson<LLMResponse>(rawJson);
            if (!string.IsNullOrEmpty(resp.type) && !string.IsNullOrEmpty(resp.content))
            {
                type    = resp.type;
                content = resp.content;
                reply   = resp.reply;
                return true;
            }
        }
        catch { /* 容错：走正则 */ }

        // 容错路径 1：正则提取
        var typeMatch = Regex.Match(rawJson,
            @"""type""\s*:\s*""(action|chat)""", RegexOptions.IgnoreCase);
        var contentMatch = Regex.Match(rawJson,
            @"""content""\s*:\s*""([^""]*)""");
        var replyMatch = Regex.Match(rawJson,
            @"""reply""\s*:\s*""([^""]*)""");

        if (typeMatch.Success && contentMatch.Success)
        {
            type    = typeMatch.Groups[1].Value.ToLower();
            content = contentMatch.Groups[1].Value;
            reply   = replyMatch.Success ? replyMatch.Groups[1].Value : null;
            return true;
        }

        // 容错路径 2：完全无法解析 → 当聊天处理
        Debug.LogWarning($"[ResponseParser] 无法解析 JSON，当作聊天处理: {rawJson}");
        type    = "chat";
        content = rawJson.Length > 100 ? rawJson[..100] : rawJson;
        return true;
    }
}
