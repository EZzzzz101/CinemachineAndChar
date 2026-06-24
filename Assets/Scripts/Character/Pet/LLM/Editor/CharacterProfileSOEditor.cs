using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// LLM 角色配置生成器 — 菜单 Tools/LLM/Character Profile Generator 打开
/// 输入角色名，LLM 自动生成 CharacterProfileSO
/// </summary>
public class CharacterProfileSOEditor : EditorWindow
{
    private CharacterProfileSO _targetProfile;
    private string _characterDescription = "";
    private string _endpointUrl = "http://127.0.0.1:7860/api/llm";
    private bool   _isGenerating;
    private string _statusMessage;
    private Vector2 _scrollPos;

    [MenuItem("Tools/LLM/Character Profile Generator")]
    public static void ShowWindow()
    {
        var window = GetWindow<CharacterProfileSOEditor>("角色配置生成器");
        window.minSize = new Vector2(400, 340);
        window.Show();
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("角色配置生成器", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "输入角色名或描述（如\"绝区零的妮可\"、\"原神的派蒙\"），\n" +
            "LLM 自动填充 CharacterProfileSO 的全部字段。\n\n" +
            "需要先启动 Python 后端: python server.py\n" +
            "并确保 LM Studio Local Server 已开启（端口 1234）。",
            MessageType.Info);

        // ── 目标 SO ──
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("目标配置", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        _targetProfile = (CharacterProfileSO)EditorGUILayout.ObjectField(
            _targetProfile, typeof(CharacterProfileSO), false);
        if (GUILayout.Button("新建", GUILayout.Width(50)))
        {
            _targetProfile = CreateNewProfile();
        }
        EditorGUILayout.EndHorizontal();

        // ── LLM 设置 ──
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("LLM 设置", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("端点地址", GUILayout.Width(60));
        _endpointUrl = EditorGUILayout.TextField(_endpointUrl);

        // ── 角色描述 ──
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("角色描述", EditorStyles.boldLabel);
        _characterDescription = EditorGUILayout.TextField(_characterDescription);

        // ── 生成按钮 ──
        EditorGUILayout.Space(8);
        GUI.enabled = !_isGenerating && _targetProfile != null && !string.IsNullOrWhiteSpace(_characterDescription);
        if (GUILayout.Button(_isGenerating ? "生成中..." : "生成角色配置", GUILayout.Height(32)))
        {
            Generate();
        }
        GUI.enabled = true;

        // ── 状态 ──
        if (!string.IsNullOrEmpty(_statusMessage))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(_statusMessage,
                _statusMessage.StartsWith("生成成功") ? MessageType.Info : MessageType.Warning);
        }

        EditorGUILayout.EndScrollView();

        if (GUI.changed && _targetProfile != null)
            EditorUtility.SetDirty(_targetProfile);
    }

    private void Generate()
    {
        string prompt = CharacterProfileSO.BuildGenerationPrompt(_characterDescription);
        string jsonBody = $"{{\"prompt\":\"{EscapeJson(prompt)}\",\"max_tokens\":512}}";

        _isGenerating = true;
        _statusMessage = "正在请求 LLM...";
        Repaint();

        SendEditorRequest(_endpointUrl, jsonBody, (success, responseText) =>
        {
            if (!success)
            {
                _statusMessage = $"请求失败: {responseText}";
                _isGenerating = false;
                Repaint();
                return;
            }

            string innerJson = UnwrapContent(responseText);

            _targetProfile.PopulateFromJson(innerJson);
            EditorUtility.SetDirty(_targetProfile);
            AssetDatabase.SaveAssetIfDirty(_targetProfile);

            _statusMessage = "生成成功，字段已填充，请检查";
            _isGenerating = false;
            Repaint();
        });
    }

    private static CharacterProfileSO CreateNewProfile()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "新建 CharacterProfile",
            "CharacterProfile", "asset",
            "选择保存路径");
        if (string.IsNullOrEmpty(path)) return null;

        var profile = CreateInstance<CharacterProfileSO>();
        AssetDatabase.CreateAsset(profile, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = profile;
        return profile;
    }

    // ─────────────── 网络 + 解析（不变）───────────────

    private static void SendEditorRequest(string url, string jsonBody, Action<bool, string> callback)
    {
        var req = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 30;

        req.SendWebRequest();

        EditorApplication.CallbackFunction check = null;
        check = () =>
        {
            if (!req.isDone) return;
            EditorApplication.update -= check;

            bool ok = req.result == UnityWebRequest.Result.Success;
            callback(ok, ok ? req.downloadHandler.text : req.error);
            req.Dispose();
        };
        EditorApplication.update += check;
    }

    private static string UnwrapContent(string raw)
    {
        raw = raw.Trim();
        try
        {
            var wrapper = JsonUtility.FromJson<ContentWrapper>(raw);
            if (wrapper != null && !string.IsNullOrEmpty(wrapper.content))
                return wrapper.content;
        }
        catch { }
        return raw;
    }

    [Serializable]
    private class ContentWrapper
    {
        public string content;
    }

    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
    }
}
