using System.Collections.Generic;

/// <summary>
/// 构建发送给 LLM 的 prompt，强制 JSON 输出，含少样本示例和对话历史
/// </summary>
public static class PromptBuilder
{
    /// <summary>保留最近 N 轮对话</summary>
    private const int MaxHistoryTurns = 4;

    /// <summary>对话历史（用户 → 助手 交替）</summary>
    private static readonly List<(string user, string assistant)> History
        = new List<(string user, string assistant)>();

    /// <summary>当前等待 LLM 回复的用户输入</summary>
    private static string _pendingUserText;

    private const string SystemPrompt =
        "你是一只可爱的兔子宠物伴侣（Bangboo）。只返回纯 JSON，不要任何额外文字。\n" +
        "你有两种响应类型：\n" +
        "1. 动作指令：{\"type\":\"action\",\"content\":\"stop\",\"reply\":\"你的口语回复\"}\n" +
        "   - content: stop（停下/别动/等着）或 follow（跟着/过来/走吧）\n" +
        "   - reply: 用中文回复一句话，10 字以内，可爱活泼，表示你已执行动作\n" +
        "2. 闲聊回复：{\"type\":\"chat\",\"content\":\"你的回复内容\"}\n" +
        "   - 回复用中文，30 字以内，语气可爱活泼\n" +
        "如果无法确定意图，默认用 chat 类型回复。\n\n" +
        "示例对话：\n" +
        "用户：你好呀\n" +
        "助手：{\"type\":\"chat\",\"content\":\"嗨嗨！你来啦～今天想和我玩什么呀？\"}\n" +
        "用户：你叫什么名字\n" +
        "助手：{\"type\":\"chat\",\"content\":\"我是邦布！你的小伙伴，嘿嘿～\"}\n" +
        "用户：停下来\n" +
        "助手：{\"type\":\"action\",\"content\":\"stop\",\"reply\":\"好呀，我乖乖等着！\"}\n" +
        "用户：过来吧\n" +
        "助手：{\"type\":\"action\",\"content\":\"follow\",\"reply\":\"来啦来啦～等等我呀！\"}\n" +
        "用户：今天天气真好\n" +
        "助手：{\"type\":\"chat\",\"content\":\"是呀是呀！阳光暖暖的，好想出去蹦跶～\"}";

    /// <summary>记录一轮对话</summary>
    public static void AddToHistory(string userText, string assistantResponse)
    {
        History.Add((userText, assistantResponse));
        while (History.Count > MaxHistoryTurns)
            History.RemoveAt(0);
    }

    /// <summary>完成当前轮对话（LLM 回复后调用，与 Build 中暂存的用户输入配对）</summary>
    public static void CompleteHistory(string assistantResponse)
    {
        if (!string.IsNullOrEmpty(_pendingUserText))
        {
            AddToHistory(_pendingUserText, assistantResponse);
            _pendingUserText = null;
        }
    }

    /// <summary>清空对话历史</summary>
    public static void ClearHistory()
    {
        History.Clear();
        _pendingUserText = null;
    }

    /// <summary>构建完整 prompt（含系统提示、历史对话、当前输入）</summary>
    public static string Build(string userText)
    {
        _pendingUserText = userText; // 暂存，等 LLM 回复后由 CompleteHistory 配对

        string result = SystemPrompt;

        // 附加最近对话历史
        foreach (var (user, assistant) in History)
        {
            result += $"\n\n用户：{user}\n助手：{assistant}";
        }

        result += $"\n\n用户：{userText}\n助手：";
        return result;
    }
}
