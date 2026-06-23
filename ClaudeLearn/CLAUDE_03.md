# CLAUDE_03 — AI 宠物对话系统（2026-06-21）

> **⚠️ Claude：请先阅读所有 `CLAUDE*.md` 文件（按数字顺序），了解完整学习过程，不要重复已纠正过的错误。**

---

## 本次学习内容：LLM 驱动的 AI 宠物系统

### 架构全貌

```
Unity 客户端 ←HTTP JSON→ Python FastAPI ← llama.cpp ← Qwen3.5-4B Q4_K_M
```

```
ChatInputUI (底部输入框，Enter 唤出/发送，Esc 关闭)
    ↓
LLMClient (协程异步 POST，请求锁 + 2s 冷却)
    ↓
Python FastAPI (127.0.0.1:7860/api/llm)
    ↓
Qwen3.5-4B 推理 → JSON 返回
    ↓
ResponseParser (JsonUtility + 正则容错)
    ↓
BangbooBrain (宠物主控：跟随/停止/聊天叠加)
    ├── BangbooFollow (跟随逻辑，Flower 参数控制动画)
    ├── SpeechBubble (头顶气泡，World Space Canvas，Billboard)
    └── Animator (兔子.controller)
```

---

## 关键设计决策

### 1. Chat 是叠加态，不是独立状态

聊天不覆盖移动逻辑——跟随中聊天 = 边走边显示气泡，待机中聊天 = 原地显示气泡。

```csharp
// BangbooBrain.cs
private bool _isChatting;        // 叠加标记
private bool _lastPreChatFollow; // 进入聊天前是否在移动

public void EnterChatMode(string text)
{
    _lastPreChatFollow = _isMoving;  // 存现场
    _isChatting = true;
    _animator.SetBool("Chat", true);
    _speechBubble.Show(text);
}

public void ExitChatMode()
{
    _isChatting = false;
    _animator.SetBool("Chat", false);
    _isMoving = _lastPreChatFollow;  // 恢复现场
}
```

### 2. 输入控制：只禁 Move/Fire/Dash，保留 CameraLook

之前用 `SwitchCurrentActionMap("UI")` 会把相机也禁掉。改为单独禁用动作：

```csharp
// ChatInputUI.cs
_moveAction = _playerInput.actions.FindAction("Move");
_fireAction = _playerInput.actions.FindAction("Fire");
_dashAction = _playerInput.actions.FindAction("Dash");

// Show 时
_moveAction?.Disable();
_fireAction?.Disable();
_dashAction?.Disable();
```

这样打字时 CameraLook 不受影响，视角依然可以拖动。

### 3. TMP 输入框交互：Enter 先唤出，再发送

```
默认隐藏 → Enter 唤出输入框 → 打字 → Enter 发送
                                  → Esc 退出
```

TMP_InputField 设为 SingleLine 模式，Enter 自动触发 onSubmit → Send()。
如果输入框失焦了（等 LLM 回复期间），再按 Enter 先重新聚焦，再按 Enter 发送。

### 4. SpeechBubble 全自动创建

不需要手动建 Canvas 子对象。挂上 SpeechBubble 组件，Awake 里自动创建：

```csharp
// 自动创建 World Space Canvas + 背景 + TMP_Text
// ContentSizeFitter 让气泡大小跟随文字自适应
// 最大宽度 _maxWidth = 250，超过自动换行变高
```

Billboard 用 `Quaternion.Slerp` 平滑朝向摄像机（只绕 Y 轴，不仰头）。

### 5. LLMClient 防护机制

```csharp
// 请求锁：上一个请求没回来，不发新的
if (_isRequestInFlight) return;

// 冷却：两次请求间隔至少 2 秒（保护显存）
if (Time.time - _lastRequestTime < _cooldownDuration) return;
```

### 6. JSON 解析容错

```csharp
// ResponseParser.cs
// ① 直接 JsonUtility.FromJson
// ② 失败 → 正则提取 "type":"xxx" 和 "content":"xxx"
// ③ 全失败 → 当聊天处理，原文字符串放进气泡
```

---

## Python 服务端要点

- FastAPI + llama-cpp-python
- Qwen3.5-4B Q4_K_M（约 2.7GB，6G 显存安全）
- n_ctx=1024, n_gpu_layers=22, max_tokens=128
- 端口 7860，地址 127.0.0.1
- Prompt 里反复强调"只返回纯 JSON"，两种格式：
  - `{"type":"action","content":"stop/follow"}`
  - `{"type":"chat","content":"回复内容"}`

---

## 踩过的坑

1. **TMP 中文全是方块** → LiberationSans 不含中文，换微软雅黑或用 SmileySans 的 Dynamic 模式
2. **Enter 发送无效** → TMP_InputField 吃掉了 Enter 事件，不能用 `Keyboard.current` 检测，要用 TMP 自带的 `onSubmit`
3. **Anaconda pip 报错** → conda 自带的 pip 太老，用 `python -m venv venv` 建隔离环境
4. **huggingface-cli 已废弃** → 换 `hf` 命令
5. **模型太大不能 Push** → `.gitignore` 加 `*.gguf`，每台机器自己下载
6. **动画自带位移 + 脚本位移冲突** → Bangboo 用 root motion 驱动位移，脚本只负责 `FaceTarget()` 转向
7. **Apply Root Motion 导致飞天** → Y 轴位移累积，勾 `Bake Into Pose` 解决
8. **Chat 过渡不全** → 控制器只有 Special_Idle → Chat，OpenDoor 缺 Chat 过渡。需要手动在 Animator 窗口加 `OpenDoor →(Chat=true)→ Idle_Chat` 和 `Idle_Chat →(Chat=false)→ Special_Idle`

---

## 新增文件清单

```
Assets/Scripts/Character/Pet/
├── BangbooBrain.cs           宠物主控
├── LLM/
│   ├── LLMClient.cs          HTTP 请求
│   ├── PromptBuilder.cs      Prompt 构建
│   └── ResponseParser.cs     JSON 解析 + 容错
└── UI/
    ├── SpeechBubble.cs       头顶气泡（自适应大小）
    └── ChatInputUI.cs        底部输入框
Server/
├── server.py                 FastAPI 后端
├── requirements.txt          依赖列表
└── README.md                 环境搭建指南
```

## 新 Animator 参数

| 参数 | 类型 | 默认 | 用途 |
|------|------|------|------|
| Flower | Bool | true | true=奔跑，false=特殊待机 |
| Chat | Bool | false | true=聊天动画 |
