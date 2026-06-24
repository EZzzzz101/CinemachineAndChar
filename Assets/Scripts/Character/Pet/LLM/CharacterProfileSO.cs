using System;
using UnityEngine;

/// <summary>
/// LLM 角色配置 ScriptableObject — 右键 → Create → LLM → CharacterProfile 创建
/// 不同角色换不同的 .asset 就能切换对话风格、动作、示例
/// </summary>
[CreateAssetMenu(fileName = "CharacterProfile", menuName = "LLM/CharacterProfile")]
public class CharacterProfileSO : ScriptableObject
{
    [Header("角色身份")]
    [Tooltip("角色名称，会出现在系统提示词中")]
    public string characterName = "邦布";

    [Tooltip("角色设定描述，告诉 LLM 它是谁")]
    [TextArea(3, 8)]
    public string personality = "你是一只可爱的兔子宠物伴侣（Bangboo）。";

    [Tooltip("语气描述，用于提示 LLM 回复风格")]
    public string tone = "可爱活泼";

    [Header("回复限制")]
    [Tooltip("闲聊回复最大字数")]
    [Range(10, 200)]
    public int maxChatLength = 30;

    [Tooltip("动作执行后口语回复最大字数")]
    [Range(5, 50)]
    public int maxReplyLength = 10;

    [Header("可用动作")]
    [Tooltip("描述 LLM 可执行的动作，每行一个，格式: 动作名: 说明")]
    [TextArea(2, 6)]
    public string actionDescriptions = "stop: 停下/别动/等着\nfollow: 跟着/过来/走吧";

    [Header("少样本示例")]
    [Tooltip("给 LLM 的对话示例，帮它理解格式和语气")]
    public FewShotExample[] fewShotExamples;

    /// <summary>
    /// 构建完整的系统提示词
    /// </summary>
    public string BuildSystemPrompt()
    {
        // 规则部分
        string prompt =
            $"{personality}\n" +
            $"只返回纯 JSON，不要任何额外文字。\n\n" +
            $"## 动作指令（优先判断）\n" +
            $"当用户让你执行行为时，必须返回 action：\n" +
            $"{{\"type\":\"action\",\"content\":\"动作名\",\"reply\":\"你的口语回复\"}}\n" +
            $"可用动作: {actionDescriptions}\n" +
            $"reply: 中文, {maxReplyLength}字以内, {tone}\n\n" +
            $"## 闲聊回复（无动作意图时）\n" +
            $"{{\"type\":\"chat\",\"content\":\"你的回复内容\"}}\n" +
            $"回复: 中文, {maxChatLength}字以内, {tone}\n\n" +
            $"## 判断规则\n" +
            $"- 先判断用户是否有动作意图 → 有则必须用 action\n" +
            $"- 无动作意图 → 用 chat\n" +
            $"- 不确定 → 优先 action（宁可误判动作，不可忽略指令）";

        // 示例
        if (fewShotExamples != null && fewShotExamples.Length > 0)
        {
            prompt += "\n\n## 示例对话";
            foreach (var eg in fewShotExamples)
            {
                if (!string.IsNullOrEmpty(eg.user) && !string.IsNullOrEmpty(eg.assistant))
                    prompt += $"\n用户：{eg.user}\n助手：{eg.assistant}";
            }
        }

        // 末尾强调（离生成点最近，不容易被长输入冲掉）
        prompt += "\n\n记住：每次回复前先检查用户是否有动作意图。有动作必须 action，别因为对话长就忘了。";

        return prompt;
    }

    [Serializable]
    public class FewShotExample
    {
        [Tooltip("用户说的话")]
        public string user;

        [Tooltip("助手应回复的 JSON")]
        public string assistant;
    }

    // ─────────────── LLM 自动生成 ───────────────

    /// <summary>
    /// 构建一个"生成角色配置"的 prompt，发给 LLM
    /// </summary>
    /// <param name="characterDescription">用户描述的角色名（如"绝区零的妮可"）</param>
    public static string BuildGenerationPrompt(string characterDescription)
    {
        return
            "你是一个游戏角色配置生成器。根据用户提供的角色名或描述，生成完整的角色 Prompt 配置。\n" +
            "只返回纯 JSON，不要任何额外文字。\n\n" +
            "返回格式（严格按此结构）：\n" +
            "{\n" +
            "  \"characterName\": \"角色名\",\n" +
            "  \"personality\": \"你是[角色名]。[性格、背景、说话方式的详细描述]\",\n" +
            "  \"tone\": \"语气关键词（如：可爱活泼 / 狡黠傲娇 / 温柔治愈）\",\n" +
            "  \"maxChatLength\": 30,\n" +
            "  \"maxReplyLength\": 10,\n" +
            "  \"actionDescriptions\": \"stop: 停下/别动/等着\\nfollow: 跟着/过来/走吧\",\n" +
            "  \"fewShotExamples\": [\n" +
            "    {\n" +
            "      \"user\": \"你好呀\",\n" +
            "      \"assistant\": \"{\\\"type\\\":\\\"chat\\\",\\\"content\\\":\\\"口语回复（符合角色语气）\\\"}\"\n" +
            "    },\n" +
            "    {\n" +
            "      \"user\": \"停下来\",\n" +
            "      \"assistant\": \"{\\\"type\\\":\\\"action\\\",\\\"content\\\":\\\"stop\\\",\\\"reply\\\":\\\"口语回复\\\"}\"\n" +
            "    },\n" +
            "    {\n" +
            "      \"user\": \"过来\",\n" +
            "      \"assistant\": \"{\\\"type\\\":\\\"action\\\",\\\"content\\\":\\\"follow\\\",\\\"reply\\\":\\\"口语回复\\\"}\"\n" +
            "    }\n" +
            "  ]\n" +
            "}\n\n" +
            "要求：\n" +
            "- personality 用丰富的细节描述角色的性格、说话方式、背景\n" +
            "- tone 是简短的语气关键词\n" +
            "- fewShotExamples 给 4~6 组示例，必须同时包含 chat 和 action 两种类型\n" +
            "- assistant 字段里的 JSON 必须转义引号（\\\"）\n" +
            "- actionDescriptions 描述本角色可用的动作\n\n" +
            "用户：" + characterDescription;
    }

    /// <summary>
    /// 从 LLM 返回的 JSON 填充本 SO 的全部字段
    /// </summary>
    public void PopulateFromJson(string json)
    {
        try
        {
            var gen = JsonUtility.FromJson<ProfileGenResponse>(json);
            if (gen != null)
            {
                ApplyProfile(gen);
                return;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[CharacterProfileSO] JSON 主解析失败: {ex.Message}，尝试正则降级...");
        }

        // ── 降级：正则逐字段提取（LLM 输出不规范时兜底）──
        TryPopulateFallback(json);
    }

    private void ApplyProfile(ProfileGenResponse gen)
    {
        characterName      = gen.characterName ?? characterName;
        personality        = gen.personality ?? personality;
        tone               = gen.tone ?? tone;
        if (gen.maxChatLength > 0)  maxChatLength  = gen.maxChatLength;
        if (gen.maxReplyLength > 0) maxReplyLength = gen.maxReplyLength;
        if (!string.IsNullOrEmpty(gen.actionDescriptions))
            actionDescriptions = gen.actionDescriptions;
        if (gen.fewShotExamples != null && gen.fewShotExamples.Length > 0)
            fewShotExamples = System.Array.ConvertAll(gen.fewShotExamples,
                e => new FewShotExample { user = e.user, assistant = e.assistant });
    }

    private void TryPopulateFallback(string json)
    {
        int filled = 0;

        // 简单字段 — 正则提取（支持引号内有特殊字符）
        var nameMatch = System.Text.RegularExpressions.Regex.Match(json,
            "\"characterName\"\\s*:\\s*\"([^\"]*)\"");
        if (nameMatch.Success) { characterName = nameMatch.Groups[1].Value; filled++; }

        var persMatch = System.Text.RegularExpressions.Regex.Match(json,
            "\"personality\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        if (persMatch.Success) { personality = UnescapeJson(persMatch.Groups[1].Value); filled++; }

        var toneMatch = System.Text.RegularExpressions.Regex.Match(json,
            "\"tone\"\\s*:\\s*\"([^\"]*)\"");
        if (toneMatch.Success) { tone = toneMatch.Groups[1].Value; filled++; }

        var maxChat = System.Text.RegularExpressions.Regex.Match(json,
            "\"maxChatLength\"\\s*:\\s*(\\d+)");
        if (maxChat.Success && int.TryParse(maxChat.Groups[1].Value, out int mcl) && mcl > 0)
        { maxChatLength = mcl; filled++; }

        var maxReply = System.Text.RegularExpressions.Regex.Match(json,
            "\"maxReplyLength\"\\s*:\\s*(\\d+)");
        if (maxReply.Success && int.TryParse(maxReply.Groups[1].Value, out int mrl) && mrl > 0)
        { maxReplyLength = mrl; filled++; }

        var actMatch = System.Text.RegularExpressions.Regex.Match(json,
            "\"actionDescriptions\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        if (actMatch.Success) { actionDescriptions = UnescapeJson(actMatch.Groups[1].Value); filled++; }

        // ── fewShotExamples — 逐个提取 user/assistant pair ──
        var exampleBlock = System.Text.RegularExpressions.Regex.Match(json,
            "\"fewShotExamples\"\\s*:\\s*\\[(.+)\\]",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        if (exampleBlock.Success)
        {
            var pairs = System.Text.RegularExpressions.Regex.Matches(exampleBlock.Groups[1].Value,
                "\"user\"\\s*:\\s*\"([^\"]*)\"\\s*,\\s*\"assistant\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            if (pairs.Count > 0)
            {
                fewShotExamples = new FewShotExample[pairs.Count];
                for (int i = 0; i < pairs.Count; i++)
                {
                    fewShotExamples[i] = new FewShotExample
                    {
                        user      = pairs[i].Groups[1].Value,
                        assistant = UnescapeJson(pairs[i].Groups[2].Value)
                    };
                }
                filled++;
            }
        }

        Debug.Log(filled > 0
            ? $"[CharacterProfileSO] 正则降级解析完成，填充了 {filled} 个字段（LLM 原始 JSON 不规范）"
            : "[CharacterProfileSO] 正则降级也未提取到任何字段，请检查 LLM 输出");
    }

    private static string UnescapeJson(string s)
    {
        return s.Replace("\\\"", "\"")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\\", "\\");
    }

    /// <summary>LLM 返回的 profile JSON 反序列化容器</summary>
    [Serializable]
    private class ProfileGenResponse
    {
        public string characterName;
        public string personality;
        public string tone;
        public int    maxChatLength;
        public int    maxReplyLength;
        public string actionDescriptions;
        public GenExample[] fewShotExamples;
    }

    [Serializable]
    private class GenExample
    {
        public string user;
        public string assistant;
    }
}
