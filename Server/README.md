# Bangboo AI 宠物 — 服务端

## 环境搭建

```bash
cd Server
python -m venv venv
venv\Scripts\activate          # Windows
# source venv/bin/activate     # Mac
pip install -r requirements.txt
```

## 下载模型

```bash
pip install huggingface_hub
hf download QuantFactory/Qwen3.5-4B-GGUF Qwen3.5-4B-Q4_K_M.gguf --local-dir .
```

## 启动

```bash
python server.py
```

看到 `Uvicorn running on http://127.0.0.1:7860` 即成功。

## 测试

```bash
curl -X POST http://127.0.0.1:7860/api/llm -H "Content-Type: application/json" -d "{\"prompt\":\"你好\"}"
```

## 依赖

| 包 | 用途 |
|-----|------|
| fastapi | Web 框架 |
| uvicorn | 服务器 |
| llama-cpp-python | 加载 gguf 模型推理 |

## 显存要求

RTX 4050 6GB，Qwen3.5-4B Q4_K_M 约 2.7GB，显存安全。
