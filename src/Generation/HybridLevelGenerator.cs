using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Гибридный генератор уровней с ИИ + процедурной генерацией + балансировкой
/// 
/// ШАГ 1: ИИ анализирует фото (глубина, объекты, структура)
/// ШАГ 2: Процедурный алгоритм строит уровень
/// ШАГ 3: Модуль баланса проверяет и оптимизирует
/// </summary>
public class HybridLevelGenerator
{
    private readonly AIImageAnalyzer _analyzer;
    private readonly ProceduralLevelGenerator _generator;
    private readonly LevelBalancer _balancer;

    public class GenerationResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("level_id")]
        public int LevelId { get; set; }

        [JsonPropertyName("phase_1_analysis")]
        public Phase1Result Analysis { get; set; }

        [JsonPropertyName("phase_2_generation")]
        public Phase2Result Generation { get; set; }

        [JsonPropertyName("phase_3_balance")]
        public Phase3Result Balance { get; set; }

        [JsonPropertyName("final_level")]
        public LevelExport FinalLevel { get; set; }

        [JsonPropertyName("generation_time_ms")]
        public long GenerationTimeMs { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.0-Hybrid";
    }

    public class Phase1Result
    {
        [JsonPropertyName("detected_objects")]
        public int DetectedObjectsCount { get; set; }

        [JsonPropertyName("scene_complexity")]
        public float SceneComplexity { get; set; }

        [JsonPropertyName("objects")]
        public Dictionary<string, int> ObjectsByType { get; set; }

        [JsonPropertyName("average_depth")]
        public float AverageDepth { get; set; }

        [JsonPropertyName("interest_points")]
        public int InterestPointsCount { get; set; }
    }

    public class Phase2Result
    {
        [JsonPropertyName("platforms_generated")]
        public int PlatformsCount { get; set; }

        [JsonPropertyName("enemies_placed")]
        public int EnemiesCount { get; set; }

        [JsonPropertyName("collectibles_placed")]
        public int CollectiblesCount { get; set; }

        [JsonPropertyName("obstacles_placed")]
        public int ObstaclesCount { get; set; }

        [JsonPropertyName("estimated_difficulty")]
        public float EstimatedDifficulty { get; set; }
    }

    public class Phase3Result
    {
        [JsonPropertyName("is_valid")]
        public bool IsValid { get; set; }

        [JsonPropertyName("difficulty_score")]
        public float DifficultyScore { get; set; }

        [JsonPropertyName("playability_score")]
        public float PlayabilityScore { get; set; }

        [JsonPropertyName("overall_rating")]
        public float OverallRating { get; set; }

        [JsonPropertyName("issues")]
        public List<string> Issues { get; set; }

        [JsonPropertyName("suggestions")]
        public List<string> Suggestions { get; set; }
    }

    public class LevelExport
    {
        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("player_start")]
        public (int x, int y) PlayerStart { get; set; }

        [JsonPropertyName("player_end")]
        public (int x, int y) PlayerEnd { get; set; }

        [JsonPropertyName("platforms")]
        public List<PlatformExport> Platforms { get; set; }

        [JsonPropertyName("enemies")]
        public List<EnemyExport> Enemies { get; set; }

        [JsonPropertyName("collectibles")]
        public List<CollectibleExport> Collectibles { get; set; }

        [JsonPropertyName("obstacles")]
        public List<ObstacleExport> Obstacles { get; set; }

        [JsonPropertyName("difficulty")]
        public float Difficulty { get; set; }
    }

    public class PlatformExport
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("physics")]
        public PhysicsData Physics { get; set; }
    }

    public class EnemyExport
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("health")]
        public int Health { get; set; }

        [JsonPropertyName("speed")]
        public float Speed { get; set; }

        [JsonPropertyName("patrol_path")]
        public List<(int x, int y)> PatrolPath { get; set; }
    }

    public class CollectibleExport
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("value")]
        public int Value { get; set; }
    }

    public class ObstacleExport
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("damage")]
        public int Damage { get; set; }
    }

    public class PhysicsData
    {
        [JsonPropertyName("collider_type")]
        public string ColliderType { get; set; } = "box";

        [JsonPropertyName("layer")]
        public string Layer { get; set; } = "Platform";

        [JsonPropertyName("friction")]
        public float Friction { get; set; } = 0.8f;
    }

    public HybridLevelGenerator()
    {
        _analyzer = new AIImageAnalyzer();
        _generator = new ProceduralLevelGenerator();
        _balancer = new LevelBalancer();
    }

    /// <summary>
    /// Полный процесс генерации уровня
    /// </summary>
    public GenerationResult Generate(string imagePath, int levelId = 1)
    {
        var startTime = DateTime.UtcNow;
        var result = new GenerationResult { LevelId = levelId };

        try
        {
            // ===== ШАГ 1: ИИ АНАЛИЗ =====
            Console.WriteLine("📊 ШАГ 1 - ИИ анализирует фото...");
            var analysis = _analyzer.Analyze(imagePath);

            result.Analysis = new Phase1Result
            {
                DetectedObjectsCount = analysis.Objects.Count,
                SceneComplexity = analysis.Complexity,
                InterestPointsCount = analysis.InterestPoints.Count,
                AverageDepth = analysis.DepthMap.GetAverageDepth(),
                ObjectsByType = new Dictionary<string, int>
                {
                    { "platforms", analysis.Objects.Count(o => o.Type == "platform") },
                    { "obstacles", analysis.Objects.Count(o => o.Type == "obstacle") },
                    { "collectibles", analysis.Objects.Count(o => o.Type == "collectible") },
                    { "moving", analysis.Objects.Count(o => o.Type == "moving_platform") }
                }
            };

            Console.WriteLine($"  ✓ Обнаружено объектов: {analysis.Objects.Count}");
            Console.WriteLine($"  ✓ Сложность сцены: {analysis.Complexity:P}");

            // ===== ШАГ 2: ПРОЦЕДУРНАЯ ГЕНЕРАЦИЯ =====
            Console.WriteLine("\n🎮 ШАГ 2 - Процедурный генератор строит уровень...");
            var level = _generator.Generate(analysis, levelId);

            result.Generation = new Phase2Result
            {
                PlatformsCount = level.Platforms.Count,
                EnemiesCount = level.Enemies.Count,
                CollectiblesCount = level.Collectibles.Count,
                ObstaclesCount = level.Obstacles.Count,
                EstimatedDifficulty = level.EstimatedDifficulty
            };

            Console.WriteLine($"  ✓ Платформ создано: {level.Platforms.Count}");
            Console.WriteLine($"  ✓ Врагов размещено: {level.Enemies.Count}");
            Console.WriteLine($"  ✓ Ресурсов добавлено: {level.Collectibles.Count}");
            Console.WriteLine($"  ✓ Препятствий создано: {level.Obstacles.Count}");

            // ===== ШАГ 3: БАЛАНСИРОВКА =====
            Console.WriteLine("\n⚖️  ШАГ 3 - Модуль баланса проверяет уровень...");
            var balanceReport = _balancer.ValidateAndBalance(level);

            result.Balance = new Phase3Result
            {
                IsValid = balanceReport.IsValid,
                DifficultyScore = balanceReport.DifficultyScore,
                PlayabilityScore = balanceReport.PlayabilityScore,
                OverallRating = balanceReport.OverallRating,
                Issues = balanceReport.Issues,
                Suggestions = balanceReport.Suggestions
            };

            Console.WriteLine($"  ✓ Уровень валиден: {balanceReport.IsValid}");
            Console.WriteLine($"  ✓ Баланс сложности: {balanceReport.DifficultyScore:P}");
            Console.WriteLine($"  ✓ Проходимость: {balanceReport.PlayabilityScore:P}");
            Console.WriteLine($"  ✓ Общий рейтинг: {balanceReport.OverallRating:P}");

            // Показать предложения если есть
            if (balanceReport.Suggestions.Count > 0)
            {
                Console.WriteLine("  💡 Предложения:");
                foreach (var suggestion in balanceReport.Suggestions)
                {
                    Console.WriteLine($"    - {suggestion}");
                }
            }

            // ===== ЭКСПОРТ =====
            result.FinalLevel = ExportLevel(level);
            result.Success = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
            result.Success = false;

            // Initialize null results with default values to prevent NullReferenceException
            result.Analysis ??= new Phase1Result
            {
                DetectedObjectsCount = 0,
                SceneComplexity = 0,
                InterestPointsCount = 0,
                AverageDepth = 0,
                ObjectsByType = new Dictionary<string, int>()
            };

            result.Generation ??= new Phase2Result
            {
                PlatformsCount = 0,
                EnemiesCount = 0,
                CollectiblesCount = 0,
                ObstaclesCount = 0,
                EstimatedDifficulty = 0
            };

            result.Balance ??= new Phase3Result
            {
                IsValid = false,
                DifficultyScore = 0,
                PlayabilityScore = 0,
                OverallRating = 0,
                Issues = new List<string> { ex.Message },
                Suggestions = new List<string>()
            };

            result.FinalLevel ??= new LevelExport
            {
                Width = 800,
                Height = 600,
                PlayerStart = (100, 100),
                PlayerEnd = (700, 500),
                Platforms = new List<PlatformExport>(),
                Enemies = new List<EnemyExport>(),
                Collectibles = new List<CollectibleExport>(),
                Obstacles = new List<ObstacleExport>(),
                Difficulty = 0
            };
        }

        result.GenerationTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

        Console.WriteLine($"\n✅ Готово! Время генерации: {result.GenerationTimeMs}ms");

        return result;
    }

    /// <summary>
    /// Экспортирует уровень в удобный формат
    /// </summary>
    private LevelExport ExportLevel(ProceduralLevelGenerator.GeneratedLevel level)
    {
        return new LevelExport
        {
            Width = level.Width,
            Height = level.Height,
            PlayerStart = level.PlayerStart,
            PlayerEnd = level.PlayerEnd,
            Difficulty = level.EstimatedDifficulty,

            Platforms = level.Platforms.Select(p => new PlatformExport
            {
                Id = p.Id,
                X = p.X,
                Y = p.Y,
                Width = p.Width,
                Height = p.Height,
                Type = p.Type,
                Physics = new PhysicsData
                {
                    Friction = p.Type == "bouncy" ? 0.3f : p.Type == "moving" ? 0.5f : 0.8f
                }
            }).ToList(),

            Enemies = level.Enemies.Select(e => new EnemyExport
            {
                Id = e.Id,
                X = e.X,
                Y = e.Y,
                Type = e.Type,
                Health = e.Health,
                Speed = e.Speed,
                PatrolPath = e.PatrolPath
            }).ToList(),

            Collectibles = level.Collectibles.Select(c => new CollectibleExport
            {
                Id = c.Id,
                X = c.X,
                Y = c.Y,
                Type = c.Type,
                Value = c.Value
            }).ToList(),

            Obstacles = level.Obstacles.Select(o => new ObstacleExport
            {
                Id = o.Id,
                X = o.X,
                Y = o.Y,
                Width = o.Width,
                Height = o.Height,
                Type = o.Type,
                Damage = o.Damage
            }).ToList()
        };
    }

    /// <summary>
    /// Сохраняет результат в JSON файл
    /// </summary>
    public void SaveToJson(GenerationResult result, string outputPath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        string json = JsonSerializer.Serialize(result, options);
        System.IO.File.WriteAllText(outputPath, json);

        Console.WriteLine($"💾 Уровень сохранён: {outputPath}");
    }

    /// <summary>
    /// Экспортирует уровень в 3D OBJ формат
    /// </summary>
    public string Export3D_OBJ(ProceduralLevelGenerator.GeneratedLevel level)
    {
        Console.WriteLine("🎨 ШАГ 4 - 3D генератор экспортирует в OBJ...");
        return Level3DExporter.ExportToOBJ(level);
    }

    /// <summary>
    /// Экспортирует уровень в GLTF формат
    /// </summary>
    public string Export3D_GLTF(ProceduralLevelGenerator.GeneratedLevel level)
    {
        Console.WriteLine("🎨 ШАГ 4 - 3D генератор экспортирует в GLTF...");
        return Level3DExporter.ExportToGLTF(level);
    }

    /// <summary>
    /// Генерирует MTL файл материалов
    /// </summary>
    public string GenerateMTL(ProceduralLevelGenerator.GeneratedLevel level)
    {
        return TextureGenerator.GenerateSceneMaterials(level);
    }

    /// <summary>
    /// Экспортирует полный пакет (OBJ, MTL, GLTF, скрипты для Unity/Godot)
    /// </summary>
    public Dictionary<string, string> ExportFullPackage(ProceduralLevelGenerator.GeneratedLevel level)
    {
        Console.WriteLine("📦 Создание полного 3D пакета...");
        return Level3DExporter.ExportLevelPackage(level);
    }

    /// <summary>
    /// Сохраняет 3D пакет на диск
    /// </summary>
    public void Save3DPackage(ProceduralLevelGenerator.GeneratedLevel level, string outputPath)
    {
        Console.WriteLine($"💾 Сохранение 3D пакета в: {outputPath}");
        Level3DExporter.SaveLevelPackage(level, outputPath);
    }
}

