"""
Bangboo AI Pet — LLM 后端（LM Studio 适配层）
启动: python server.py   （需要先打开 LM Studio，加载 Qwen3.5-4B-Q4_K_M.gguf，开启 Local Server）
端口: 7860 → 转发到 LM Studio 1234
"""
from fastapi import FastAPI
from pydantic import BaseModel
import httpx
import uvicorn

app = FastAPI(title="Bangboo LLM Backend")

LM_STUDIO_URL = "http://127.0.0.1:1234/v1/chat/completions"

# ── 请求/响应模型（接口不变，Unity 无需改） ──
class LLMRequest(BaseModel):
    prompt: str
    max_tokens: int = 128  # 可选，默认 128（profile 生成建议传 512+）

class LLMResponse(BaseModel):
    content: str

# ── 拆分 prompt 为 system + user ──
def split_prompt(prompt: str):
    """
    PromptBuilder 格式:
      {SystemPrompt}\n\n用户：{msg}\n助手：{resp}\n\n用户：{msg}\n助手：
    拆成 system 文本 + 非 system 部分作为 user message
    """
    idx = prompt.find("用户：")
    if idx > 0:
        system = prompt[:idx].strip()
        user_part = prompt[idx:].strip()
    else:
        system = prompt
        user_part = ""

    messages = [{"role": "system", "content": system}]
    if user_part:
        messages.append({"role": "user", "content": user_part})
    return messages

# ── API ──
@app.post("/api/llm", response_model=LLMResponse)
async def llm_endpoint(req: LLMRequest):
    messages = split_prompt(req.prompt)

    async with httpx.AsyncClient(timeout=30.0) as client:
        resp = await client.post(LM_STUDIO_URL, json={
            "messages": messages,
            "max_tokens": req.max_tokens,
            "temperature": 0.7,
            "stop": ["用户：", "User:", "</s>"],
        })

    data = resp.json()
    content = data["choices"][0]["message"]["content"].strip()
    return LLMResponse(content=content)

@app.get("/health")
async def health():
    try:
        async with httpx.AsyncClient(timeout=3.0) as client:
            r = await client.get("http://127.0.0.1:1234/v1/models")
        return {"status": "ok", "lmstudio": "connected" if r.status_code == 200 else "error"}
    except Exception:
        return {"status": "ok", "lmstudio": "unreachable — 请确认 LM Studio Local Server 已开启"}

# ── 启动 ──
if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=7860)
