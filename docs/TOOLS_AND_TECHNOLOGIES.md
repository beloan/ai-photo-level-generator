# 🛠️ Инструменты и Технологии "Level Generator"

## 📊 Обзор Архитектуры

Проект **Level Generator** - это система генерации 3D уровней для видеоигр из фотографий с использованием ИИ анализа изображений.

---

## 🔧 Основные Инструменты и Библиотеки

### 1. **Backend Framework**
- **Framework**: ASP.NET Core Web API (.NET 8.0)
- **Язык**: C# 12
- **Runtime**: .NET 8.0
- **Web Server**: Kestrel (встроенный в ASP.NET Core)
- **API Type**: RESTful Web API

### 2. **Обработка Изображений**
#### SixLabors.ImageSharp 3.1.4
```
- Загрузка изображений (PNG, JPG, JPEG, JFIF, GIF)
- Преобразование форматов
- Обработка пикселей
- Анализ яркости и контуров
- Операции с редактированием изображений
```

#### SixLabors.ImageSharp.Drawing 2.1.3
```
- Векторная графика
- Рисование фигур
- Текстура и заливка
- Трансформации изображений
```

### 3. **ИИ и Компьютерное Зрение**

#### Встроенный AI Анализатор (`AIImageAnalyzer.cs`)
**Функции:**
- Построение карты глубины (depth map)
- Обнаружение объектов
- Анализ контуров и границ
- Обнаружение интересных точек (interest points)
- Расчет сложности сцены

**Методы анализа:**
- Edge Detection (обнаружение краёв)
- Color Analysis (анализ цветов)
- Depth Estimation (оценка глубины)
- Scene Complexity Calculation (расчет сложности)

### 4. **3D Генерация Геометрии**

#### Mesh3DGenerator (`Mesh3DGenerator.cs`)
- Создание базовых примитивов (cube, sphere, cylinder)
- Генерация вершин (vertices)
- Создание индексов (indices) для треугольников
- Материалы и текстуры
- Экспорт в форматы OBJ и GLTF

#### Direct3DLevelGenerator (`Direct3DLevelGenerator.cs`)
- Прямое преобразование фото → 3D уровень
- Конвертация обнаруженных объектов в 3D примитивы
- Расчет граничных боксов (bounding boxes)
- Статистика геометрии

### 5. **Обработка и Преобразование**

#### ImageProcessor (`ImageProcessor.cs`)
- Предварительная обработка изображений
- Нормализация размеров
- Цветовые преобразования

#### TextureGenerator (`TextureGenerator.cs`)
- Генерация текстур из исходных изображений
- Применение текстур к 3D моделям

### 6. **Экспорт Форматов**

**Поддерживаемые форматы:**

1. **OBJ (Wavefront)**
   - Вершины, грани, текстурные координаты
   - MTL файлы для материалов
   - Универсальный 3D формат

2. **GLTF (GL Transmission Format)**
   - Современный 3D формат
   - Поддержка анимации
   - Оптимизирован для веба

3. **Package (ZIP)**
   - Полный пакет в одном файле
   - Включает все форматы и манифесты

### 7. **Frontend Technologies**

#### HTML5
- Семантическая разметка
- Drag & Drop API
- File Input API

#### CSS3
- Modern CSS Grid & Flexbox
- Animations & Transitions
- Responsive Design
- Custom Properties (переменные)

#### JavaScript (ES6+)
```
- Fetch API (HTTP запросы)
- FormData API (загрузка файлов)
- JSON парсинг
- DOM Manipulation
- Event Handling
```

### 8. **Сетевые Протоколы**

| Протокол | Использование |
|----------|---------------|
| HTTP/HTTPS | REST API запросы |
| JSON | Обмен данными |
| multipart/form-data | Загрузка файлов |
| CORS | Cross-origin запросы |

### 9. **API Endpoints (REST)**

```
┌─ Generation ─────────────────────────────────
│ POST /api/generate
│ ├─ Input: Image file (PNG, JPG, JFIF)
│ └─ Output: 3D level data + statistics
│
├─ 3D Export ──────────────────────────────────
│ POST /api/export-3d/obj
│ │  └─ Returns: OBJ model data
│ POST /api/export-3d/gltf
│ │  └─ Returns: GLTF model data
│ POST /api/export-3d/package
│ │  └─ Returns: ZIP package
│
└─ Health Check ───────────────────────────────
  GET /api/health
     └─ Returns: Server status
```

### 10. **Версионирование и Конфигурация**

- **appsettings.json**: Конфигурация приложения
- **Диплом.csproj**: Project manifest
- **Диплом.sln**: Solution file
- **Версия API**: 3.0-Direct3D

---

## 📦 Структура Проекта

```
Диплом/
├── Program.cs ........................ ASP.NET Core хост и endpoints
├── Direct3DLevelGenerator.cs ......... 3D генерация из фото
├── Mesh3DGenerator.cs ............... 3D примитивы и геометрия
├── AIImageAnalyzer.cs ............... ИИ анализ изображений
├── TextureGenerator.cs .............. Генерация текстур
├── ImageProcessor.cs ................ Обработка изображений
├── wwwroot/
│   ├── index.html ................... Главная страница
│   ├── app.js ....................... Логика фронтенда
│   ├── style.css .................... Стили
│   └── uploads/ ..................... Загруженные изображения
├── bin/Release/net8.0/ .............. Скомпилированный exe
└── Диплом.csproj .................... Project dependencies
```

---

## 🚀 Процесс Генерации

```
1. Загрузка Image (JPEG/PNG/JFIF)
   ↓
2. ИИ Анализ (AIImageAnalyzer)
   - Depth Map
   - Object Detection
   - Scene Properties
   ↓
3. Конвертация → 3D (Direct3DLevelGenerator)
   - Object → Mesh conversion
   - Primitive creation
   ↓
4. Экспорт (OBJ/GLTF/Package)
   ↓
5. Отправка клиенту (JSON response)
```

---

## 🔌 Зависимости NuGet

| Package | Version | Назначение |
|---------|---------|-----------|
| SixLabors.ImageSharp | 3.1.4 | Обработка изображений |
| SixLabors.ImageSharp.Drawing | 2.1.3 | Векторная графика |
| Microsoft.NET.Sdk.Web | 8.0 | Web Framework |

---

## 💻 Требования Системы

| Компонент | Требование |
|-----------|-----------|
| OS | Windows 10+ |
| Runtime | .NET 8.0 SDK |
| Browser | Chrome/Firefox/Edge (современные) |
| RAM | 512 MB минимум |
| Disk | 500 MB (с зависимостями) |
| Network | HTTP/HTTPS доступ |

---

## 📌 Ключевые Особенности

### ✅ Поддерживаемые Форматы Входа
- PNG (RGBA, RGB)
- JPG / JPEG
- JFIF (автоматическая конвертация)
- GIF

### ✅ Выходные Форматы
- OBJ (Wavefront 3D)
- GLTF (GL Transmission Format)
- ZIP Package (все форматы)
- JSON (статистика и метаданные)

### ✅ Анализируемые Объекты
- Платформы (platforms)
- Препятствия (obstacles)
- Собираемые предметы (collectibles)
- Поверхности (surfaces)

---

## 🎯 Использованные Паттерны

| Паттерн | Применение |
|---------|-----------|
| MVC | ASP.NET Core Web API |
| Builder | Создание mesh объектов |
| Pipeline | Обработка изображений |
| Strategy | Различные методы экспорта |
| Factory | Создание 3D примитивов |

---

## 📊 Метрики и Статистика

**Собираемые метрики:**
- Количество сеток (meshes)
- Количество вершин (vertices)
- Количество треугольников (triangles)
- Обнаруженные платформы
- Обнаруженные препятствия
- Сложность сцены (0-1)
- Объем сцены (кубические единицы)
- Время генерации (мс)

---

## 🔐 Безопасность

- **CORS**: Настроен для всех источников (AllowAll)
- **Validation**: Проверка типов файлов
- **Error Handling**: Обработка всех исключений
- **Logging**: Все действия логируются

---

## ⚡ Производительность

- **Direct 3D Mode**: Пропускает 2D обработку
- **Streaming**: Пиксельная обработка (не в памяти целиком)
- **Caching**: Временное кеширование результатов
- **Async/Await**: Асинхронная обработка запросов

---

## 📝 Команды Запуска

```bash
# Сборка
dotnet build -c Release

# Запуск в режиме Development
dotnet run -c Debug

# Запуск в Production
dotnet run -c Release

# Запуск через exe
.\bin\Release\net8.0\Диплом.exe

# Тестирование API
curl -X POST http://localhost:5000/api/generate -F "file=@image.jpg"
```

---

## 🌐 Web Interface

**URL**: http://localhost:5000

**Функции:**
- 📸 Загрузка изображений (drag-drop или клик)
- 👁️ Предпросмотр выбранного изображения
- 🎨 Генерация 3D модели из фото
- 📊 Просмотр статистики
- 💾 Экспорт в OBJ/GLTF/ZIP
- ▶️ Просмотр игры (Play Game)

---

## 📚 Дополнительные Ресурсы

### Документация
- [ASP.NET Core Docs](https://docs.microsoft.com/en-us/aspnet/core/)
- [SixLabors ImageSharp](https://sixlabors.com/products/imagesharp/)
- [Wavefront OBJ Format](https://en.wikipedia.org/wiki/Wavefront_.obj_file)
- [GLTF Specification](https://www.khronos.org/gltf/)

### Примечания
- Версия API: **3.0-Direct3D**
- Режим работы: **Photo → AI Analysis → 3D Model**
- Статус: **Production Ready**

---

*Документ создан: 2026-04-08*
*Версия проекта: 3.0-Direct3D*
