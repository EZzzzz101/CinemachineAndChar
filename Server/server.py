"""
Bangboo AI Pet — LLM 后端
FastAPI + llama-cpp-python + Qwen2-7B-Instruct Q4_K_M
启动: python server.py
端口: 7860
"""
from fastapi import FastAPI
from pydantic import BaseModel
from llama_cpp import Llama
import uvicorn

app = FastAPI(title="Bangboo LLM Backend")

# ── 模型加载（参数适配 RTX 4050 6GB 显存） ──
llm = Llama(
    model_path="Qwen3.5-4B-Q4_K_M.gguf",
    n_ctx=1024,           # 上下文窗口
    n_gpu_layers=22,      # GPU 加速层数（勿超过 22）
    verbose=False,
)

# ── 请求/响应模型 ──
class LLMRequest(BaseModel):
    prompt: str

class LLMResponse(BaseModel):
    content: str

# ── API ──
@app.post("/api/llm", response_model=LLMResponse)
async def llm_endpoint(req: LLMRequest):
    output = llm(
        req.prompt,
        max_tokens=128,
        temperature=0.7,
        stop=["</s>", "用户：", "User:"],
    )
    content = output["choices"][0]["text"].strip()
    return LLMResponse(content=content)

@app.get("/health")
async def health():
    return {"status": "ok"}

# ── 启动 ──
if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=7860)
