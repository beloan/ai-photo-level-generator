# ai_service — ИИ-сервис оценки глубины (Depth Anything V2)

Python-микросервис на FastAPI. По фотографии возвращает карту глубины, которую .NET-бэкенд
использует вместо старой «яркость = высота». Это и есть нейросетевая («ИИ») часть проекта.

- **Модель:** `depth-anything/Depth-Anything-V2-Small-hf` (предобучена, Apache-2.0, ~25M параметров).
- **Запуск:** CPU. Веса (~100 МБ) скачиваются из Hugging Face при первом старте, затем берутся из кэша.

## Установка и запуск

```powershell
cd ai_service
py -3.11 -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
uvicorn app:app --host 127.0.0.1 --port 8001
```

Порт **8001** должен совпадать с `appsettings.json` → `DepthService.BaseUrl`.

## Эндпоинты

- `GET /health` → `{status, model, device, loaded}`
- `POST /depth` (multipart-форма, поле `file`) → `{model, width, height, grid_w, grid_h, depth[]}`
  - `depth` — нормализованная карта глубины (0..1, где **1 = ближе**), сетка `grid_w × grid_h`,
    пропорции исходного фото сохранены.

Пример:
```powershell
curl -X POST -F "file=@../input.png" http://127.0.0.1:8001/depth
```

## Переменные окружения
- `DEPTH_MODEL` — id модели (по умолчанию Depth-Anything-V2-Small-hf).
- `DEPTH_GRID_MAX` — макс. сторона выходной сетки (по умолчанию 160).
