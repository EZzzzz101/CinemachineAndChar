# LLM 对话系统架构文档

## 一、脚本总览

| 脚本 | 位置 | 职责 |
|------|------|------|
| `ChatInputUI.cs` | `Scripts/Character/Pet/UI/` | 屏幕底部输入框，Enter 发送，Esc 关闭 |
| `LLMClient.cs` | `Scripts/Character/Pet/LLM/` | HTTP 客户端，协程发请求，事件回调 |
| `PromptBuilder.cs` | `Scripts/Character/Pet/LLM/` | 静态类，拼系统提示词 + 对话历史 + 用户输入 |
| `ResponseParser.cs` | `Scripts/Character/Pet/LLM/` | 解析 LLM 返回的 JSON，正则容错 |
| `BangbooBrain.cs` | `Scripts/Character/Pet/` | 宠物主控，协调跟随/聊天/LLM 响应 |
| `CharacterProfileSO.cs` | `Scripts/Character/Pet/LLM/` | ScriptableObject，角色配置（性格、示例、动作） |
| `CharacterProfileSOEditor.cs` | `Scripts/Character/Pet/LLM/Editor/` | Editor 窗口，一键调用 LLM 生成角色配置 |
| `SpeechBubble.cs` | `Scripts/Character/Pet/UI/` | 世界空间气泡，显示对话和"思考中"动画 |
| `server.py` | `Server/` | Python 中间层，翻译 Unity 请求 ↔ LM Studio |

---

## 二、整体流程

```
用户键盘输入 → ChatInputUI → LLMClient.SendRequest()
                                   │
                            PromptBuilder.Build()
                            （拼系统提示词 + 历史 + 输入）
                                   │
                            POST → server.py (7860)
                                   │
                            split_prompt() 拆成 system + user
                                   │
                            POST → LM Studio (1234)
                                   │
                            拿到 LLM 回复
                                   │
                            ResponseParser.TryParse()
                                   │
                      ┌─────────┴─────────┐
                   action                  chat
              BangbooBrain             SpeechBubble
              执行动作                   显示文字
```

---

## 三、输入处理

**入口：** `ChatInputUI.Send()`

用户按 Enter 后，从 `TMP_InputField` 取文本，同时：
- 在玩家角色头顶显示气泡（`SpeechBubble.Show(text)`）
- 禁用 Move / Fire / Dash 输入（防止对话时误操作）
- 保留 CameraLook（可转视角）

**数据流：** 纯文本 `string` → `LLMClient.SendRequest(text)`

---

## 四、数据格式

### 4.1 发送格式

Unity → server.py：

```json
{"prompt": "你是一只可爱的兔子...\n\n用户：你是谁\n助手：", "max_tokens": 128}
```

### 4.2 server.py → LM Studio

`split_prompt()` 以第一个 `用户：` 为界切分：
- 前面 → `{"role": "system", "content": "..."}`
- 后面 → `{"role": "user", "content": "..."}`

发给 LM Studio 的 OpenAI 兼容格式：

```json
{
  "messages": [
    {"role": "system", "content": "你是一只可爱的兔子..."},
    {"role": "user", "content": "用户：你是谁\n助手："}
  ],
  "max_tokens": 128,
  "temperature": 0.7,
  "stop": ["用户：", "User:", "</s>"]
}
```

### 4.3 LLM 返回格式

LLM 输出的 JSON（两种类型）：

```json
// 动作指令
{"type":"action","content":"stop","reply":"好呀，我乖乖等着！"}

// 闲聊回复
{"type":"chat","content":"嗨嗨！你来啦～"}
```

`ResponseParser.TryParse()` 支持三层容错：
1. `JsonUtility.FromJson` 直接解析
2. 正则提取（容错畸形 JSON）
3. 兜底当 chat 处理（完全无法解析时）

---

## 五、网络传输

**LLMClient.SendCoroutine()** — Unity 协程异步请求：

```csharp
using (var req = new UnityWebRequest(url, "POST"))
{
    req.uploadHandler   = new UploadHandlerRaw(bodyRaw);   // 请求体
    req.downloadHandler = new DownloadHandlerBuffer();      // 响应缓冲区
    req.SetRequestHeader("Content-Type", "application/json");
    req.timeout = 15;

    yield return req.SendWebRequest();  // 挂起等响应，不卡主线程

    // 唤醒后读 req.downloadHandler.text
}
```

关键机制：
- `yield return` 挂起协程，Unity 继续渲染游戏
- HTTP 响应到达后自动唤醒续跑
- `_isRequestInFlight` 锁防重复请求
- `_cooldownDuration`（2s）冷却防刷屏

---

## 六、连接建立

### 启动顺序

```
1. LM Studio          加载模型 → 开启 Local Server（端口 1234）
2. python server.py   启动 FastAPI（端口 7860）
3. Unity Editor       运行游戏
```

### server.py 核心逻辑

```python
@app.post("/api/llm")
async def llm_endpoint(req: LLMRequest):
    messages = split_prompt(req.prompt)         # 拆 system + user
    resp = await client.post(LM_STUDIO_URL,     # 转发 LM Studio
        json={"messages": messages, ...})
    content = resp.json()["choices"][0]["message"]["content"]
    return {"content": content}                  # 包装返回 Unity
```

---

## 七、响应处理

**BangbooBrain.HandleLLMResponse(bool success, string rawContent)**

```
success == false → 打印错误，忽略
success == true  → ResponseParser.TryParse(rawContent)
                    │
                    ├── type="action" → HandleAction(content, reply)
                    │        ├── "stop"   → 停止跟随
                    │        └── "follow" → 恢复跟随
                    │
                    └── type="chat"   → EnterChatMode(content)
                             └── 显示气泡，播放聊天动画
```

处理完调用 `PromptBuilder.CompleteHistory(reply)` 记录对话历史。

---

## 八、提示词优化

### 8.1 SystemPrompt 结构

`CharacterProfileSO.BuildSystemPrompt()` 按以下层次构建：

```
角色设定（personality）

## 动作指令（优先判断）
可用动作 + 格式规范

## 闲聊回复（无动作意图时）
格式规范

## 判断规则
- 先判断动作意图 → 必须用 action
- 不确定 → 优先 action（而非默认 chat）

## 示例对话（few-shot examples）
用户：xx / 助手：xx

记住：每次回复前先检查用户是否有动作意图。
```

### 8.2 长输入防遗忘

`PromptBuilder.Build()` 在 `助手：` 前插入提醒：

```
用户：{输入内容}
（先判断是否有动作意图，有则必须用 action）
助手：
```

### 8.3 动作词库扩充

通过 `actionDescriptions` 字段增加触发变体：

```
stop: 停下/别动/等着/站住/别乱跑/别愣着/等我一会
follow: 跟着/过来/走吧/快跟上/跟我来/别落下
```

### 8.4 对话历史滑动窗口

只保留最近 4 轮（`MaxHistoryTurns = 4`），防止 prompt 过长超出模型上下文。

---

## 九、Editor 工具 — LLM 生成角色配置

### 打开方式

`Tools → LLM → Character Profile Generator`

### 原理

输入角色名（如"绝区零的妮可"），Editor 工具：

1. `CharacterProfileSO.BuildGenerationPrompt(角色名)` 构建专用 prompt
2. 通过 `EditorApplication.update` 轮询方式发 HTTP 请求到 7860
3. server.py 转发 LM Studio，生成结构化的角色 JSON
4. `UnwrapContent()` 解包 `{"content":"..."}` 外层
5. `PopulateFromJson()` 用 `JsonUtility.FromJson` 解析填入 SO 字段
6. `EditorUtility.SetDirty` + `AssetDatabase.SaveAssetIfDirty` 保存

### Editor 异步请求

由于 Editor 中无协程，改用轮询模式：

```csharp
req.SendWebRequest();
EditorApplication.CallbackFunction check = null;
check = () => {
    if (!req.isDone) return;
    EditorApplication.update -= check;   // 完成，取消轮询
    callback(ok, req.downloadHandler.text);
};
EditorApplication.update += check;       // 每帧检查
```

### 生成的角色 JSON 格式

```json
{
  "characterName": "妮可",
  "personality": "你是绝区零的妮可，性格狡黠傲娇...",
  "tone": "狡黠、傲娇、毒舌",
  "maxChatLength": 30,
  "maxReplyLength": 10,
  "actionDescriptions": "stop: 停下/别动\nfollow: 跟着/过来",
  "fewShotExamples": [
    {"user": "你好呀", "assistant": "{\"type\":\"chat\",\"content\":\"哼，又来一个...\"}"},
    ...
  ]
}
```

---

## 十、文件清单

```
Assets/Scripts/Character/Pet/
├── BangbooBrain.cs                 # 宠物主控
├── BangbooFollow.cs                # 旧版跟随脚本
├── LLM/
│   ├── LLMClient.cs                # HTTP 客户端
│   ├── PromptBuilder.cs            # Prompt 构建器
│   ├── ResponseParser.cs           # 响应解析器
│   ├── CharacterProfileSO.cs       # 角色配置 SO
│   └── Editor/
│       └── CharacterProfileSOEditor.cs  # Editor 生成工具
└── UI/
    ├── ChatInputUI.cs              # 输入框
    └── SpeechBubble.cs             # 对话气泡

Server/
└── server.py                       # Python 中间层
```
