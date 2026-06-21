"""
ИИ-сервис оценки глубины (monocular depth estimation).

Модель: Depth Anything V2 Small (depth-anything/Depth-Anything-V2-Small-hf),
предобученная, открытая (Apache-2.0), запускается на CPU.

Назначение: по одному фото отдаёт карту глубины. .NET-бэкенд берёт её вместо
старой «яркость = высота» и строит из неё рельеф/высоты платформ.

Эндпоинты:
  GET  /health  — статус и имя модели
  POST /depth   — multipart-форма с файлом изображения -> карта глубины (сетка 0..1)
"""
import io
import os

import numpy as np
from fastapi import FastAPI, File, HTTPException, UploadFile
from PIL import Image

MODEL_ID = os.getenv("DEPTH_MODEL", "depth-anything/Depth-Anything-V2-Small-hf")
GRID_MAX = int(os.getenv("DEPTH_GRID_MAX", "160"))  # макс. сторона сетки глубины

app = FastAPI(title="Depth Anything V2 Service")

# Ленивая инициализация: тяжёлые импорты и загрузка весов — при старте.
_pipe = None
_device = "cpu"


def _get_pipe():
    global _pipe, _device
    if _pipe is None:
        import torch
        from transformers import pipeline

        _device = "cuda" if torch.cuda.is_available() else "cpu"
        print(f"[depth] Загружаю модель {MODEL_ID} на {_device} ...", flush=True)
        _pipe = pipeline("depth-estimation", model=MODEL_ID, device=_device)
        print("[depth] Модель загружена.", flush=True)
    return _pipe


@app.get("/health")
def health():
    return {"status": "ok", "model": MODEL_ID, "device": _device, "loaded": _pipe is not None}


@app.post("/warmup")
def warmup():
    _get_pipe()
    return {"status": "ok", "model": MODEL_ID, "device": _device, "loaded": True}


@app.post("/depth")
async def depth(file: UploadFile = File(...)):
    try:
        raw = await file.read()
        image = Image.open(io.BytesIO(raw)).convert("RGB")
    except Exception as exc:
        raise HTTPException(status_code=400, detail=f"Не удалось прочитать изображение: {exc}")

    W, H = image.width, image.height

    pipe = _get_pipe()
    result = pipe(image)
    depth_img = result["depth"]  # PIL Image, нормализована 0..255 (ярче = ближе)

    arr = np.asarray(depth_img, dtype=np.float32)
    mn, mx = float(arr.min()), float(arr.max())
    norm = np.zeros_like(arr) if (mx - mn) < 1e-6 else (arr - mn) / (mx - mn)

    # Уменьшаем до компактной сетки с сохранением пропорций (1 = ближе, 0 = дальше).
    if W >= H:
        gw = GRID_MAX
        gh = max(1, round(GRID_MAX * H / W))
    else:
        gh = GRID_MAX
        gw = max(1, round(GRID_MAX * W / H))

    grid = np.asarray(
        Image.fromarray((norm * 255).astype(np.uint8)).resize((gw, gh), Image.BILINEAR),
        dtype=np.float32,
    ) / 255.0

    return {
        "model": MODEL_ID,
        "width": W,
        "height": H,
        "grid_w": gw,
        "grid_h": gh,
        "depth": [round(float(v), 4) for v in grid.flatten()],
    }
