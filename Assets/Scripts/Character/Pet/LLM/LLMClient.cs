using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// LLM HTTP 客户端 — 协程异步请求，带请求锁和冷却
/// 挂在 Bangboo GameObject 上
/// </summary>
public class LLMClient : MonoBehaviour
{
    [Header("后端地址")]
    [SerializeField] private string _endpointUrl = "http://127.0.0.1:7860/api/llm";

    [Header("请求控制")]
    [Tooltip("两次请求最小间隔（秒）")]
    [SerializeField] private float _cooldownDuration = 2f;

    private float _lastRequestTime = -99f;
    private bool  _isRequestInFlight;

    /// <summary>请求发出时触发（接收到用户输入，开始等待 LLM）</summary>
    public event Action OnThinkingStarted;

    /// <summary>bool = 是否成功, string = 响应原文或错误信息</summary>
    public event Action<bool, string> OnResponseReceived;

    /// <summary>发送请求（冷却中或请求进行中会被忽略）</summary>
    public void SendRequest(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText)) return;

        if (Time.time - _lastRequestTime < _cooldownDuration)
        {
            Debug.Log("[LLMClient] 请求冷却中，已忽略");
            return;
        }

        if (_isRequestInFlight)
        {
            Debug.Log("[LLMClient] 上一个请求未完成，已忽略");
            return;
        }

        string prompt = PromptBuilder.Build(userText);
        _lastRequestTime = Time.time;
        _isRequestInFlight = true;
        OnThinkingStarted?.Invoke();
        StartCoroutine(SendCoroutine(prompt));
    }

    private IEnumerator SendCoroutine(string prompt)
    {
        string jsonBody = $"{{\"prompt\":\"{EscapeJson(prompt)}\"}}";

        using (var req = new UnityWebRequest(_endpointUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 15;

            yield return req.SendWebRequest();

            _isRequestInFlight = false;

            if (req.result != UnityWebRequest.Result.Success)
            {
                OnResponseReceived?.Invoke(false, req.error);
                yield break;
            }

            string responseText = req.downloadHandler.text;
            // 解析 {"content": "..."}
            try
            {
                //反序列化JSON->c#
                var wrapper = JsonUtility.FromJson<ResponseWrapper>(responseText);
                OnResponseReceived?.Invoke(true, wrapper.content);
            }
            catch
            {
                OnResponseReceived?.Invoke(true, responseText);
            }
        }
    }

    /// <summary>转义 JSON 字符串中的特殊字符</summary>
    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
    }

    [Serializable]
    private class ResponseWrapper
    {
        public string content;
    }
}
