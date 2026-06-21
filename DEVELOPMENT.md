# Руководство по проекту

Гайд для работы с этим репозиторием. Описывает идею проекта, реальную
архитектуру (что подключено, а что лежит «про запас»), стек, команды запуска и известные
проблемы.

---

## 1. Что это за проект

Практическая часть ВКР на тему **«ИИ-система генерации целых уровней на основе реальных
фотографий с сохранением игрового баланса»**.

Идея: пользователь загружает фотографию → **нейросеть Depth Anything V2** оценивает глубину
сцены по фото (+ классический CV для объектов) → по сцене строится игровой уровень: 2D-набор
платформ и/или 3D-геометрия (меши), которые можно посмотреть в браузере (Three.js) и
экспортировать (OBJ/GLTF) для движка (Unity).

> ⚠️ **Важно про «игровой баланс».** В работе заявлен баланс уровня, и в коде есть
> отдельный модуль `LevelBalancer.cs` (проверка проходимости, кривой сложности,
> распределения ресурсов). **Но в основной веб-конвейер он сейчас НЕ подключён** — см.
> раздел 4. Это главный разрыв между текстом ВКР и реальным кодом, который надо держать в
> голове при доработке.

---

## 2. Стек технологий

| Слой | Технология |
|------|-----------|
| Бэкенд / API | **.NET 8**, ASP.NET Core **Minimal API** (`Program.cs`, top-level statements) |
| Обработка изображений | **SixLabors.ImageSharp** 3.1.4 (+ Drawing 2.1.3) |
| ИИ-модель | Python **FastAPI** + **Depth Anything V2 Small** (оценка глубины, предобучена, Apache-2.0) — `ai_service/` |
| Фронтенд | Статика в `wwwroot/` — HTML/CSS/JS + **Three.js 0.128** (CDN) |
| Контейнеризация | `Dockerfile`, `docker-compose.yml` |

Требования: **.NET 8 SDK**; для ИИ-модели — **Python 3.11** с пакетами из
`ai_service/requirements.txt` (torch + transformers, ставится в `ai_service/.venv`).

---

## 3. Как собрать и запустить

Рабочая директория: корень репозитория. Имя проекта — `Диплом.csproj` (кириллица в имени,
это нормально).

### Веб-приложение (основной сценарий)

```powershell
dotnet build Диплом.csproj -c Debug      # сборка
dotnet run --project Диплом.csproj       # запуск
```

Сервер поднимается на **http://0.0.0.0:5000** (порт задан в `appsettings.json` → `Kestrel`).
Открыть в браузере: **http://localhost:5000** — отдаётся `wwwroot/index.html`.

Скрипты-обёртки (`start_server.bat`, `RUN_SERVER.bat` и пр.) удалены при чистке: они
дублировали `dotnet run` и содержали устаревший хардкод пути (`C:\Диплом`). Запуск — командой
`dotnet run` выше.

Проверка живости:
```powershell
curl http://localhost:5000/api/health
# {"status":"Online","version":"3.0-Direct3D",...}
```

### ИИ-сервис глубины (Depth Anything V2)

Даёт реальную карту глубины по фото. Без него приложение работает: при недоступности сервиса
`/api/generate` **автоматически откатывается** на классическую глубину по яркости
(`AIImageAnalyzer`).

```powershell
cd ai_service
py -3.11 -m venv .venv; .venv\Scripts\activate
pip install -r requirements.txt
uvicorn app:app --host 0.0.0.0 --port 8001
```

- Порт **8001** должен совпадать с `appsettings.json` → `DepthService.BaseUrl`
  (`http://127.0.0.1:8001`).
- Модель `depth-anything/Depth-Anything-V2-Small-hf` (~100 МБ) скачивается из Hugging Face при
  первом старте; дальше грузится из кэша. Запускается на CPU.
- Отключить обращение к сервису — `DepthService.Enabled: false` в `appsettings.json`.
- Эндпоинты сервиса: `GET /health`, `POST /depth` (multipart `file`) → карта глубины.

### Docker

```powershell
docker compose up        # см. docker-compose.yml
```

---

## 4. Архитектура: что РЕАЛЬНО выполняется

Основной поток запроса (`POST /api/generate`, см. `Program.cs`):

```
Фото (upload)
  → SaveUploadedImageAsync()          # сохранение, конвертация .jfif/.jpg → .png
  → [опц.] DepthEstimationClient      # POST в ai_service → реальная карта глубины (Depth Anything V2)
  → AIImageAnalyzer.Analyze(depth)    # глубина из ИИ (или по яркости, если сервис выкл.) + flood-fill объекты
  → Direct3DLevelGenerator            # объекты → 3D-меши (кубы-платформы/препятствия)
  → BuildLevelFromAnalysis()          # объекты → 2D LevelDataContainer (платформы)
  → JSON ответ (geometry_stats, level_2d, analysis, depth_grid, detailed_objects, analysis_source)
```

Фронтенд (`wwwroot/app.js` + `viewer-3d.js`) рисует результат в Three.js, показывает
статистику и умеет экспорт OBJ/GLTF и «World Model» сессии.

> Все исходники C# лежат в `src/` по областям: `Analysis/`, `Generation/`, `Ai/`, `Models/`,
> `World/`. Точка входа `Program.cs` — в корне. Проект SDK-style → компилирует все `.cs` рекурсивно,
> поэтому раскладка по папкам на сборку не влияет.

### Классы, которые ПОДКЛЮЧЕНЫ к API

- `src/Analysis/AIImageAnalyzer.cs` — классический анализ изображения (depth/edge/flood-fill).
- `src/Analysis/AdvancedObjectClassifier.cs` — классификация объекта по цвету/форме/глубине.
- `src/Generation/Direct3DLevelGenerator.cs` — **главный генератор**, фото → 3D-геометрия (2D пропускается).
- `src/Generation/Mesh3DGenerator.cs`, `src/Generation/TextureGenerator.cs` — меши и текстуры.
- `src/Models/LevelData.cs` — модели `Vector2D`, `PlatformData`, `LevelDataContainer` (2D-формат уровня).
- `src/Ai/DepthEstimationClient.cs`, `DepthServiceOptions.cs` — клиент ИИ-сервиса глубины (Depth Anything V2); даёт реальную карту глубины в `AIImageAnalyzer`.
- `src/World/WorldModelEngine.cs` — «Yan-style» Sim/Gen/Edit мир для эндпоинтов `/api/world/*`.

### Классы, которые НЕ подключены к веб-API (компилируются, но не вызываются из `Program.cs`)

> «Вторая половина» заявленной архитектуры. Если задача — приблизить код к теме ВКР (баланс), начинать отсюда.

- `src/Generation/LevelBalancer.cs` — **балансировка и валидация уровня** (проходимость BFS, кривая
  сложности, тупики, распределение ресурсов). Ядро «игрового баланса». Работает с `ProceduralLevelGenerator.GeneratedLevel`.
- `src/Generation/ProceduralLevelGenerator.cs` — процедурная генерация уровня (платформы, враги, бонусы, препятствия).
- `src/Generation/HybridLevelGenerator.cs` — гибрид: анализ фото + процедурка + баланс. По смыслу
  «целый уровень с балансом», но из API не вызывается.
- `src/Generation/Level3DExporter.cs`, `src/Generation/EnhancedTextureManager.cs`,
  `src/Analysis/AdvancedImageAnalyzer.cs` — вспомогательные/альтернативные реализации.

> Мусор и заглушки (`*.reference.cs` Unity-стабы, бэкапы `*.cs.bak/.backup`, пустой `ImageProcessor.cs`,
> логи) удалены при чистке (отправлены в Корзину).

---

## 5. HTTP API (Minimal API в `Program.cs`)

| Метод | Путь | Назначение |
|-------|------|-----------|
| POST | `/api/generate` | Фото → ИИ-глубина (Depth Anything V2, с откатом на яркость) → 3D. Главный эндпоинт. |
| POST | `/api/export-3d/obj` | Экспорт сгенерированной геометрии в OBJ. |
| POST | `/api/export-3d/gltf` | Экспорт в GLTF (сейчас заглушка-каркас, без буферов). |
| POST | `/api/export-3d/package` | OBJ+GLTF+manifest одним пакетом. |
| POST | `/api/world/init` \| `/step` \| `/edit` \| `/save` \| `/load` | «World Model» сессии (`WorldModelEngine`). |
| POST | `/api/save-level`, GET `/api/get-level` | Хранение текущего уровня в памяти сервера. |
| GET | `/api/health` | Статус. |

Все запросы с файлом — `multipart/form-data`, первый файл формы. Тестовая картинка в корне:
`input.png` (есть также `Без названия*.jfif/png`).

Пример:
```powershell
curl -X POST -F "imageFile=@input.png" http://localhost:5000/api/generate
```

---

## 6. Карта репозитория (кратко)

```
Program.cs                      # точка входа, все HTTP-эндпоинты
appsettings.json                # порт Kestrel (5000), DepthService, LevelGeneration
Диплом.csproj / Диплом.sln      # проект .NET 8 (SDK=Web); компилирует все .cs рекурсивно
DEVELOPMENT.md / README.md      # документация (DEVELOPMENT.md — источник истины)
Dockerfile / docker-compose.yml # контейнеризация
src/                            # исходники C#:
  Analysis/                     #   анализ изображения (CV + классификация)
  Generation/                   #   3D / процедурка / баланс / экспорт
  Ai/                           #   клиент ИИ-сервиса глубины (Depth Anything V2)
  Models/                       #   модели данных уровня
  World/                        #   World Model engine
wwwroot/                        # фронтенд: index.html, app.js, viewer-3d.js, game.*, style.css
ai_service/                     # python: app.py (FastAPI) — Depth Anything V2, requirements.txt
scripts/                        # make_samples.py — генератор тест-картинок
docs/                           # технические заметки (ARCHITECTURE, API_EXAMPLES, TOOLS… и т.п.)
samples/                        # тест-изображения для генерации (см. samples/README.md)
bin/ obj/                       # артефакты сборки
input.png, level.json, output.png   # примеры входа/результата
```

Чистка проекта: отчётные/маркетинговые `.md` (`COMPLETION_REPORT.md`, `SENIOR_DEV_REPORT.md`,
`FINAL_REPORT.md`, `INDEX.md` и т.п.) и материалы ВКР (`*.docx`, `*.pdf`, `*.pptx`) **удалены в
Корзину Windows** (восстановимо). Источник истины — **код** и этот файл. Несколько полезных
технических заметок оставлены в `docs/`. Демо-видео `*.mp4` оставлены в корне.

---

## 7. Известные проблемы и нюансы

- **ИИ-глубина подключена**: `/api/generate` зовёт `ai_service` (Depth Anything V2) за реальной
  картой глубины; она идёт в `AIImageAnalyzer.Analyze(..., externalDepth)`. Если сервис
  выключен/недоступен — откат на глубину по яркости. Поле ответа `analysis_source` =
  `depth-anything-v2` либо `classic-cv`. **YOLO полностью удалён** (не подходил под задачу).
- **46 warnings** при сборке — в основном `CS8618`/`CS8625` (nullable reference types) и
  `NU1902/NU1903` (security advisories на `SixLabors.ImageSharp 3.1.4`). На работу не влияют;
  при желании можно обновить ImageSharp и проставить nullable-аннотации.
- **[ИСПРАВЛЕНО]** OBJ-экспорт (`Direct3DLevelGenerator.ExportMeshesToOBJ`) форматировал
  координаты по текущей локали → на русской системе выводил десятичную **запятую**
  (`v -5,0000 ...`), из-за чего OBJ был невалиден и не импортировался в Blender/Unity.
  Переведён на `FormattableString.Invariant` → теперь точка (`v -5.0000 ...`).
- **GLTF-экспорт — заглушка**: `ExportMeshesToGLTF` отдаёт пустой каркас glTF без реальных
  буферов геометрии. OBJ-экспорт — полноценный.
- **Баланс не в конвейере** (см. раздел 4) — ключевая точка для доработки под тему ВКР.
- **Реалистичный рельеф** (доработка графики) живёт в `wwwroot/viewer-3d.js`: при наличии
  реальной глубины от модели (`analysis_source = depth-anything-v2`) высота сетки берётся
  из неё (билинейная выборка + сглаживание), иначе — из яркости фото. Фото драпируется
  текстурой. Переключение источника — `Viewer3D.setReliefMode('depth'|'photo')`. Сравнение
  до/после — в `docs/renders/` (см. README там).

---

## 8. Подсказки для дальнейшей работы

- Менять поведение API → `Program.cs`. Логика анализа → `AIImageAnalyzer.cs` /
  `AdvancedObjectClassifier.cs`. 3D → `Direct3DLevelGenerator.cs` + `Mesh3DGenerator.cs`.
- Чтобы «подружить» код с темой про баланс: подключить `HybridLevelGenerator` /
  `LevelBalancer` к новому эндпоинту и возвращать `BalanceReport` в ответе.
- Параметры генерации (мин. длина платформы, пороги объединения, таймаут) — в
  `appsettings.json` → `LevelGeneration` (читаются не везде, проверять по коду).
- Тестовый прогон без фронтенда — `curl` по `/api/health` и `/api/generate` с `input.png`.
- Окружение: Windows, основная оболочка — PowerShell; в имени проекта кириллица — пути с
  `Диплом.csproj` указывать в кавычках.
