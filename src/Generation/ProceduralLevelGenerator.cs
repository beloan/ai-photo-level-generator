using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Процедурная генерация уровня на основе ИИ анализа
/// Размещает платформы, врагов, ресурсы и точки интереса
/// </summary>
public class ProceduralLevelGenerator
{
    private static readonly Random _random = new Random();

    public class GeneratedLevel
    {
        public int LevelId { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public List<PlatformElement> Platforms { get; set; } = new();
        public List<EnemyElement> Enemies { get; set; } = new();
        public List<CollectibleElement> Collectibles { get; set; } = new();
        public List<ObstacleElement> Obstacles { get; set; } = new();
        public (int x, int y) PlayerStart { get; set; }
        public (int x, int y) PlayerEnd { get; set; }
        public float EstimatedDifficulty { get; set; } // 0-1
        public int Checksum { get; set; } // Для валидации
    }

    public class PlatformElement
    {
        public int Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Type { get; set; } // "normal", "moving", "bouncy", "fragile"
        public bool IsTemporary { get; set; }
        public float MovementSpeed { get; set; } // Для moving платформ
    }

    public class EnemyElement
    {
        public int Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Type { get; set; } // "walker", "flyer", "turret"
        public List<(int x, int y)> PatrolPath { get; set; } = new();
        public int Health { get; set; }
        public float Speed { get; set; }
    }

    public class CollectibleElement
    {
        public int Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Type { get; set; } // "coin", "crystal", "powerup"
        public int Value { get; set; }
    }

    public class ObstacleElement
    {
        public int Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Type { get; set; } // "spike", "fire", "acid", "box"
        public int Damage { get; set; }
    }

    /// <summary>
    /// Генерирует уровень на основе ИИ анализа
    /// </summary>
    public GeneratedLevel Generate(AIImageAnalyzer.SceneAnalysis analysis, int levelId = 1)
    {
        var level = new GeneratedLevel
        {
            LevelId = levelId,
            Width = 800,
            Height = 600,
            PlayerStart = (100, 500)
        };

        // Фаза 1: Разместить основные платформы на основе обнаруженных объектов
        GeneratePlatforms(analysis, level);

        // Фаза 2: Разместить врагов с учетом сложности
        GenerateEnemies(analysis, level);

        // Фаза 3: Добавить собираемые предметы
        GenerateCollectibles(analysis, level);

        // Фаза 4: Добавить препятствия
        GenerateObstacles(analysis, level);

        // Фаза 5: Определить точку выхода
        level.PlayerEnd = DetermineLevelEnd(level);

        // Фаза 6: Рассчитать сложность
        level.EstimatedDifficulty = CalculateDifficulty(level, analysis);

        // Фаза 7: Рассчитать контрольную сумму
        level.Checksum = CalculateChecksum(level);

        return level;
    }

    /// <summary>
    /// Генерирует платформы на основе обнаруженных объектов
    /// </summary>
    private void GeneratePlatforms(AIImageAnalyzer.SceneAnalysis analysis, GeneratedLevel level)
    {
        int platformId = 1;

        // Добавить стартовую платформу
        level.Platforms.Add(new PlatformElement
        {
            Id = platformId++,
            X = (int)level.PlayerStart.x - 50,
            Y = (int)level.PlayerStart.y,
            Width = 100,
            Height = 20,
            Type = "normal"
        });

        // Преобразовать обнаруженные объекты в платформы (если есть)
        if (analysis?.Objects != null && analysis.Objects.Count > 0 && analysis.DepthMap?.Width > 0 && analysis.DepthMap?.Height > 0)
        {
            foreach (var obj in analysis.Objects.Where(o => o.Type != "shadow"))
            {
                // Масштабировать координаты из изображения в пространство уровня
                float scaleX = level.Width / (float)analysis.DepthMap.Width;
                float scaleY = level.Height / (float)analysis.DepthMap.Height;

                int x = (int)(obj.X * scaleX);
                int y = (int)(obj.Y * scaleY);
                int width = Math.Max(40, (int)(obj.Width * scaleX));
                int height = Math.Max(15, (int)(obj.Height * scaleY));

                // Определить тип платформы
                string platformType = MapObjectTypeToPlatformType(obj.Type, obj.AverageDepth);

                level.Platforms.Add(new PlatformElement
                {
                    Id = platformId++,
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    Type = platformType,
                    MovementSpeed = platformType == "moving" ? _random.Next(2, 5) : 0
                });
            }
        }

        // Добавить процедурно сгенерированные платформы для заполнения пропусков
        AddFillerPlatforms(level, analysis?.Complexity ?? 0.5f);

        // Убедиться, что платформы не перекрываются
        RemoveOverlappingPlatforms(level);

        // ГАРАНТИРОВАТЬ минимально 3 платформы
        if (level.Platforms.Count < 3)
        {
            // Если недостаточно платформ после удаления дубликатов, восстановить базовые
            int missingCount = 3 - level.Platforms.Count;
            for (int i = 0; i < missingCount; i++)
            {
                level.Platforms.Add(new PlatformElement
                {
                    Id = level.Platforms.Count + 1,
                    X = 150 + (i * 200),
                    Y = 300 + (i * 100),
                    Width = 80,
                    Height = 20,
                    Type = "normal"
                });
            }
        }
    }

    /// <summary>
    /// Добавляет платформы для заполнения пропусков между основными
    /// </summary>
    private void AddFillerPlatforms(GeneratedLevel level, float complexity)
    {
        // Количество платформ-заполнителей зависит от сложности
        int fillerCount = (int)(10 + complexity * 20);

        for (int i = 0; i < fillerCount; i++)
        {
            int x = _random.Next(0, level.Width - 60);
            int y = _random.Next(150, level.Height - 100);
            int width = _random.Next(40, 120);

            // Проверить, не перекрывается ли со существующими
            if (!PlatformsOverlap(level.Platforms, x, y, width, 20))
            {
                level.Platforms.Add(new PlatformElement
                {
                    Id = level.Platforms.Count + 1,
                    X = x,
                    Y = y,
                    Width = width,
                    Height = 20,
                    Type = "normal"
                });
            }
        }
    }

    /// <summary>
    /// Генерирует врагов на основе сложности
    /// </summary>
    private void GenerateEnemies(AIImageAnalyzer.SceneAnalysis analysis, GeneratedLevel level)
    {
        // Если платформ недостаточно, не добавляем врагов
        if (level.Platforms.Count < 2) return;

        int enemyCount = (int)(2 + analysis.Complexity * 8);
        int enemyId = 1;

        for (int i = 0; i < enemyCount; i++)
        {
            // Размещать врагов на платформах (не в пустоте)
            var platform = level.Platforms[_random.Next(level.Platforms.Count)];
            
            var enemy = new EnemyElement
            {
                Id = enemyId++,
                X = platform.X + _random.Next(0, Math.Max(1, platform.Width - 30)),
                Y = platform.Y - 40,
                Type = SelectEnemyType(analysis.Complexity),
                Health = 3,
                Speed = _random.Next(2, 5)
            };

            // Создать патрульный путь
            GeneratePatrolPath(enemy, platform, level);

            level.Enemies.Add(enemy);
        }
    }

    /// <summary>
    /// Выбирает тип врага в зависимости от сложности
    /// </summary>
    private string SelectEnemyType(float complexity)
    {
        float rand = (float)_random.NextDouble();
        
        if (complexity > 0.7f)
        {
            return rand < 0.3f ? "flyer" : rand < 0.7f ? "turret" : "walker";
        }
        else if (complexity > 0.4f)
        {
            return rand < 0.5f ? "flyer" : "walker";
        }
        else
        {
            return "walker";
        }
    }

    /// <summary>
    /// Генерирует путь патруля врага
    /// </summary>
    private void GeneratePatrolPath(EnemyElement enemy, PlatformElement platform, GeneratedLevel level)
    {
        // Враг ходит туда-сюда на платформе
        enemy.PatrolPath.Add((platform.X, platform.Y - 40));
        enemy.PatrolPath.Add((platform.X + Math.Min(100, platform.Width - 30), platform.Y - 40));
    }

    /// <summary>
    /// Генерирует собираемые предметы
    /// </summary>
    private void GenerateCollectibles(AIImageAnalyzer.SceneAnalysis analysis, GeneratedLevel level)
    {
        int collectibleCount = (int)(5 + analysis.Complexity * 15);
        int collectibleId = 1;

        // Не более 50 предметов чтобы не перегрузить уровень
        collectibleCount = Math.Min(collectibleCount, 50);

        for (int i = 0; i < collectibleCount; i++)
        {
            // Размещать на платформах и в пустом пространстве
            int x = _random.Next(50, level.Width - 50);
            int y = _random.Next(100, level.Height - 100);

            level.Collectibles.Add(new CollectibleElement
            {
                Id = collectibleId++,
                X = x,
                Y = y,
                Type = _random.NextDouble() > 0.8 ? "crystal" : "coin",
                Value = _random.Next(1, 5)
            });
        }
    }

    /// <summary>
    /// Генерирует препятствия
    /// </summary>
    private void GenerateObstacles(AIImageAnalyzer.SceneAnalysis analysis, GeneratedLevel level)
    {
        // Если нет платформ, не добавляем препятствия
        if (level.Platforms.Count == 0) return;

        int obstacleCount = (int)(2 + analysis.Complexity * 5);
        int obstacleId = 1;

        for (int i = 0; i < obstacleCount; i++)
        {
            var platform = level.Platforms[_random.Next(level.Platforms.Count)];

            level.Obstacles.Add(new ObstacleElement
            {
                Id = obstacleId++,
                X = platform.X + _random.Next(20, Math.Max(21, platform.Width - 40)),
                Y = platform.Y - 30,
                Width = 30,
                Height = 30,
                Type = _random.Next(0, 3) switch
                {
                    0 => "spike",
                    1 => "fire",
                    _ => "acid"
                },
                Damage = _random.Next(1, 3)
            });
        }
    }

    /// <summary>
    /// Определяет точку выхода (несколько платформ от конца)
    /// </summary>
    private (int x, int y) DetermineLevelEnd(GeneratedLevel level)
    {
        if (level.Platforms.Count == 0) return (level.Width - 100, 100);

        // Найти самую далекую платформу от стартовой позиции
        var endPlatform = level.Platforms
            .OrderByDescending(p => 
                Math.Sqrt(Math.Pow(p.X - level.PlayerStart.x, 2) + 
                          Math.Pow(p.Y - level.PlayerStart.y, 2)))
            .FirstOrDefault();

        if (endPlatform == null)
            return (level.Width - 100, 100);

        return (endPlatform.X + endPlatform.Width / 2, endPlatform.Y - 50);
    }

    /// <summary>
    /// Рассчитывает сложность уровня
    /// </summary>
    private float CalculateDifficulty(GeneratedLevel level, AIImageAnalyzer.SceneAnalysis analysis)
    {
        float platformDensity = level.Platforms.Count / 50f;
        float enemyFactor = level.Enemies.Count / 10f;
        float obstacleFactor = level.Obstacles.Count / 5f;

        // Взвешенная комбинация
        return Math.Min((platformDensity * 0.4f + enemyFactor * 0.35f + obstacleFactor * 0.25f), 1f);
    }

    /// <summary>
    /// Рассчитывает контрольную сумму для валидации
    /// </summary>
    private int CalculateChecksum(GeneratedLevel level)
    {
        int checksum = level.LevelId;
        checksum ^= level.Platforms.Count * 31;
        checksum ^= level.Enemies.Count * 17;
        checksum ^= level.Collectibles.Count * 13;
        return checksum;
    }

    // === Вспомогательные методы ===

    private string MapObjectTypeToPlatformType(string objectType, float depth)
    {
        return objectType switch
        {
            "moving_platform" => "moving",
            "obstacle" => "fragile",
            "collectible" => "bouncy",
            _ => depth > 0.7f ? "normal" : "bouncy"
        };
    }

    private bool PlatformsOverlap(List<PlatformElement> platforms, int x, int y, int w, int h)
    {
        return platforms.Any(p => 
            !(x + w < p.X || x > p.X + p.Width ||
              y + h < p.Y || y > p.Y + p.Height));
    }

    private void RemoveOverlappingPlatforms(GeneratedLevel level)
    {
        // Никогда не удалять стартовую платформу (ID первая)
        var startPlatform = level.Platforms.FirstOrDefault();
        if (startPlatform == null) return;

        for (int i = level.Platforms.Count - 1; i > 0; i--)  // Начинаем с конца, пропускаем первую (стартовая)
        {
            for (int j = i - 1; j >= 0; j--)
            {
                var p1 = level.Platforms[i];
                var p2 = level.Platforms[j];

                // Если платформы перекрываются более чем на 50%, удалить меньшую
                int overlapX = Math.Min(p1.X + p1.Width, p2.X + p2.Width) - Math.Max(p1.X, p2.X);
                int overlapY = Math.Min(p1.Y + p1.Height, p2.Y + p2.Height) - Math.Max(p1.Y, p2.Y);
                
                if (overlapX <= 0 || overlapY <= 0) continue; // Нет пересечения

                float overlapArea = overlapX * overlapY;

                if (overlapArea > (p1.Width * p1.Height * 0.5f))
                {
                    if (i > 0 && p1.Width * p1.Height < p2.Width * p2.Height)
                    {
                        level.Platforms.RemoveAt(i);
                        i--;
                        break;
                    }
                    else if (j > 0)
                    {
                        level.Platforms.RemoveAt(j);
                    }
                }
            }
        }
    }
}
