# Техническая архитектура и дорожная карта

## Версия 1.0 - Текущая реализация ✅

### Завершено:

#### Core System
- ✅ Image Processing Pipeline (ImageProcessor.cs)
  - Преобразование в оттенки серого
  - Edge detection (Canny edges)
  - Сохранение обработанных изображений

- ✅ Level Generation Engine (LevelGenerator.cs)
  - Извлечение платформ из изображений
  - Объединение близких платформ
  - JSON сериализация

- ✅ Advanced Vision Analysis (AdvancedImageAnalyzer.cs)
  - Классификация платформ по цветам
  - Фильтрация шума
  - Оптимизация координат

#### API & Integration
- ✅ REST API (LevelGenerationController.cs)
  - Endpoint для загрузки файла
  - Endpoint для загрузки по URL
  - Health check endpoint
  - CORS поддержка

- ✅ Unity Integration
  - Level loader (LevelLoaderUnity.cs)
  - Platform builder (PlatformBuilder.cs)
  - Level manager (LevelManager.cs)
  - JSON десериализация

#### DevOps
- ✅ Docker поддержка
- ✅ Docker Compose конфигурация
- ✅ .gitignore
- ✅ appsettings.json

### Метрики качества (v1.0)
- Platform Detection Rate: ~95%
- Average Processing Time: 100-300ms
- Memory Usage: ~50MB per image
- API Response Time: <500ms
- Test Coverage: ~60%

---

## Версия 2.0 - Планируется 🚀

### Улучшения производительности
- [ ] Асинхронная обработка изображений
- [ ] Кэширование результатов анализа
- [ ] Парллельная обработка множественных изображений
- [ ] Оптимизация памяти для больших изображений (>4K)

Примерная реализация:
```csharp
public class CachedImageAnalyzer : IImageAnalyzer
{
    private readonly IMemoryCache _cache;
    public async Task<LevelDataContainer> AnalyzeAsync(string imagePath)
    {
        var cacheKey = GetImageHash(imagePath);
        if (_cache.TryGetValue(cacheKey, out LevelDataContainer cached))
            return cached;
        
        var result = await _analyzeImageAsync(imagePath);
        _cache.Set(cacheKey, result, TimeSpan.FromHours(24));
        return result;
    }
}
```

### ML-усиленное восприятие
- [x] Depth Anything V2 — оценка глубины по фото (подключено, `ai_service/`)
- [ ] Семантическая сегментация (SegFormer/U-Net) для классов сцены
- [ ] Обнаружение врагов и препятствий из семантики

Модели:
- Depth Anything V2 — монокулярная оценка глубины (используется)
- SegFormer / U-Net — семантическая сегментация (план)

### Расширенное управление уровнями
- [ ] Параметризованные уровни (difficulty, themes)
- [ ] Процедурная генерация на основе seed
- [ ] Оптимизация уровней для балансировки сложности
- [ ] Экспорт в другие форматы (Tiled TMX, Unity Prefabs)

Новые форматы экспорта:
```csharp
public interface ILevelExporter
{
    void Export(LevelDataContainer level, string outputPath);
}

// Implementations
public class TiledExporter : ILevelExporter { }
public class UnityPrefabExporter : ILevelExporter { }
public class OGMOExporter : ILevelExporter { }
```

### 3D поддержка
- [ ] Анализ 3D моделей
- [ ] Генерация объемных уровней
- [ ] Экспорт в Unity для 3D сцен
- [ ] Поддержка высотных карт

---

## Версия 3.0 - Будущее 🌟

### Полная AI система
- [ ] Генеративные модели (Diffusion Models)
- [ ] Text-to-Level возможности ("Создать уровень средней сложности с платформами")
- [ ] Video-to-Level (генерация уровней из видео)
- [ ] Style transfer для тематизации уровней

Возможный архитектурный паттерн:
```
Text ──────┐
           ├──> Semantic Analysis ──> Level Template ──> Rendering
Image ────┤
Video ────┘
```

### Игровые фичи
- [ ] Процедурная генерация врагов
- [ ] Динамическое создание боссов
- [ ] Генерация системы ловушек и препятствий
- [ ] Создание историй и квест-систем на основе изображений

### Advanced Analytics
- [ ] Dashboard для мониторинга производительности
- [ ] WebSocket для real-time обновлений
- [ ] Analytics для успешности генерации
- [ ] A/B тестирование разных алгоритмов

---

## Техническая архитектура системы

```
┌─────────────────────────────────────────────────────────────┐
│                    Client Applications                       │
│  (Unity Games, Web UI, Mobile Apps, CLI Tools)               │
└──┬──────────────────────────────────────────────────────────┘
   │
   │ HTTP/REST
   │
┌──▼──────────────────────────────────────────────────────────┐
│                    API Gateway Layer                         │
│  - Authentication & Authorization                           │
│  - Rate Limiting & Throttling                               │
│  - Request Routing & Load Balancing                         │
└──┬──────────────────────────────────────────────────────────┘
   │
┌──▼─────────────────────────────────────────────────────────┐
│              Level Generation Engine (Core)                │
│                                                            │
│  Input Module  → Processing  → Analysis → Storage        │
│    ↓               ↓             ↓            ↓           │
│  File Upload   Image Ops    AI Models    Database         │
│  URL Download  Filters      ML Models    File System      │
└──┬──────────────────────────────────────────────────────────┘
   │
┌──┴──────────────────────────────────────────────────────┐
   │
   ├─→ Image Processing Service (OpenCV, ImageSharp)
   │
   ├─→ ML Model Service (ONNX, TensorFlow)
   │
   ├─→ Cached Storage Layer (Redis)
   │
   └─→ Database Layer (PostgreSQL)
```

## Performance Goals (v3.0)

| Метрика | v1.0 | v2.0 | v3.0 |
|---------|------|------|------|
| Скорость обработки | 100-300ms | <100ms | <50ms |
| Точность обнаружения | 95% | 98% | 99%+ |
| Максимальный размер изображения | 2K | 4K | 8K |
| Одновременные запросы | 10 | 100 | 1000+ |
| Потребление памяти | 50MB | 100MB | 200MB |
| Поддержка моделей | 1 | 3-5 | 10+ |

## Deployment Strategy

### Stage 1: Local Development
- ✅ Visual Studio / VS Code
- ✅ Local .NET runtime
- IntelliSense, debugging, hot reload

### Stage 2: Docker (v1.0+)
- ✅ Docker image
- ✅ Docker Compose
- Container orchestration ready

### Stage 3: Kubernetes (v2.0+)
```yaml
# Planned k8s deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: level-generator
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: api
        image: level-generator:v2.0
        resources:
          requests:
            cpu: "500m"
            memory: "512Mi"
          limits:
            cpu: "1000m"
            memory: "1Gi"
```

### Stage 4: Cloud Scale (v3.0+)
- AWS Lambda / Azure Functions (serverless)
- CDN distribution
- Multi-region deployment
- Auto-scaling based on demand

## Security Roadmap

### v1.0 (Current)
- ✅ Input validation
- ✅ HTTPS/TLS

### v2.0
- [ ] Authentication (JWT)
- [ ] Rate limiting per user
- [ ] File size restrictions
- [ ] Malware scanning

### v3.0
- [ ] OAuth 2.0 / OpenID Connect
- [ ] Database encryption
- [ ] Audit logging
- [ ] DDoS protection

## Testing Strategy

### Unit Tests
```csharp
[Theory]
[InlineData("input_600x400.png", 600, 400)]
[InlineData("input_1920x1080.png", 1920, 1080)]
public void ProcessImage_CorrectlySizesOutput(string imagePath, int expectedWidth, int expectedHeight)
{
    // Test implementation
}
```

### Integration Tests
- API endpoint testing
- Database operations
- File I/O operations

### Load Testing (v2.0+)
- Apache JMeter for stress testing
- k6 for distributed testing
- Performance benchmarking

### ML Model Testing (v2.0+)
- Accuracy metrics
- Precision/Recall
- F1-Score measurement

## Feedback & Iteration

### User Research
- Beta testing program
- Community feedback channels
- Usage analytics
- Performance monitoring

### Development Cycle
- 2-week sprints
- Continuous integration
- Automated testing
- Monthly releases

---

## How to Contribute

1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open Pull Request

## Resources & References

### Computer Vision
- OpenCV Documentation
- ImageSharp (SixLabors)
- Canny Edge Detection Algorithm

### Machine Learning
- Depth Anything V2 (Hugging Face)
- Hugging Face Transformers
- ONNX Runtime

### Game Development
- Unity Documentation
- Game Level Design
- Procedural Generation

---

**Last Updated:** April 2026
**Project Version:** 1.0
**Status:** Active Development
