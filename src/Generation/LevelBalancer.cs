using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Модуль балансировки и валидации уровней
/// Проверяет:
/// - Проходимость уровня
/// - Баланс сложности
/// - Отсутствие тупиков
/// - Справедливое распределение ресурсов
/// </summary>
public class LevelBalancer
{
    public class BalanceReport
    {
        public bool IsValid { get; set; }
        public float DifficultyScore { get; set; } // 0-1, идеально 0.5
        public float PlayabilityScore { get; set; } // 0-1, как хорошо можно пройти
        public List<string> Issues { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Suggestions { get; set; } = new();
        public float OverallRating { get; set; } // 0-1
    }

    public class PathNode
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int PlatformId { get; set; }
        public List<int> ConnectedPlatforms { get; set; } = new();
        public float DangerLevel { get; set; } // 0-1
    }

    private const int REACHABLE_DISTANCE = 150; // Максимальное расстояние прыжка
    private const int DANGER_RADIUS = 80;

    /// <summary>
    /// Проверяет и балансирует уровень
    /// </summary>
    public BalanceReport ValidateAndBalance(ProceduralLevelGenerator.GeneratedLevel level)
    {
        var report = new BalanceReport();

        // Проверка 1: Уровень проходим?
        var pathGraph = BuildPathGraph(level);
        bool isPassable = CheckPassability(level, pathGraph);

        if (!isPassable)
        {
            report.Issues.Add("Уровень не проходим - нет пути от старта к концу");
            report.IsValid = false;
        }

        // Проверка 2: Есть ли тупики?
        var deadEnds = FindDeadEnds(pathGraph);
        if (deadEnds.Count > 0)
        {
            report.Issues.Add($"Обнаружено {deadEnds.Count} тупиков");
        }

        // Проверка 3: Баланс сложности
        float diffScore = AnalyzeDifficultyPath(level, pathGraph);
        report.DifficultyScore = diffScore;

        if (diffScore < 0.3f)
        {
            report.Warnings.Add("Уровень слишком легкий");
            report.Suggestions.Add("Добавьте врагов или препятствий");
        }
        else if (diffScore > 0.75f)
        {
            report.Warnings.Add("Уровень слишком сложный");
            report.Suggestions.Add("Удалите некоторых врагов или добавьте больше платформ");
        }

        // Проверка 4: Распределение ресурсов
        float resourceScore = AnalyzeResourceDistribution(level);
        
        if (resourceScore < 0.4f)
        {
            report.Warnings.Add("Недостаточно ресурсов для сбора");
            report.Suggestions.Add("Добавьте больше монет/кристаллов");
        }

        // Проверка 5: Опасные зоны
        var dangerZones = AnalyzeDangerZones(level);
        if (dangerZones.Count > level.Enemies.Count / 2)
        {
            report.Warnings.Add("Слишком много опасных зон");
            report.Suggestions.Add("Переместите или удалите препятствия");
        }

        // Проверка 6: Расстояния между платформами
        if (!CheckPlatformSpacing(level))
        {
            report.Warnings.Add("Неравномерное расстояние между платформами");
            report.Suggestions.Add("Пересчитайте позиции платформ");
        }

        // Вычислить общий рейтинг
        float playabilityScore = CalculatePlayability(level, pathGraph);
        report.PlayabilityScore = playabilityScore;

        report.IsValid = report.Issues.Count == 0;
        report.OverallRating = (diffScore * 0.3f + playabilityScore * 0.4f + resourceScore * 0.3f);

        return report;
    }

    /// <summary>
    /// Строит граф связей между платформами
    /// </summary>
    private List<PathNode> BuildPathGraph(ProceduralLevelGenerator.GeneratedLevel level)
    {
        var pathNodes = new List<PathNode>();

        // Создать узел для каждой платформы
        for (int i = 0; i < level.Platforms.Count; i++)
        {
            var platform = level.Platforms[i];
            pathNodes.Add(new PathNode
            {
                X = platform.X + platform.Width / 2,
                Y = platform.Y,
                PlatformId = platform.Id
            });
        }

        // Найти соединённые платформы (которые можно достичь прыжком)
        for (int i = 0; i < pathNodes.Count; i++)
        {
            for (int j = 0; j < pathNodes.Count; j++)
            {
                if (i != j)
                {
                    float distance = CalculateDistance(pathNodes[i], pathNodes[j]);
                    
                    // Если платформы достижимы прыжком
                    if (distance < REACHABLE_DISTANCE && pathNodes[j].Y > pathNodes[i].Y)
                    {
                        pathNodes[i].ConnectedPlatforms.Add(j);
                        
                        // Рассчитать уровень опасности
                        pathNodes[i].DangerLevel = CalculateDanger(level, pathNodes[i], pathNodes[j]);
                    }
                }
            }
        }

        return pathNodes;
    }

    /// <summary>
    /// Проверяет, может ли игрок пройти уровень
    /// </summary>
    private bool CheckPassability(ProceduralLevelGenerator.GeneratedLevel level, List<PathNode> pathGraph)
    {
        // BFS от стартовой платформы к конечной
        var startPlatform = level.Platforms.FirstOrDefault(p => 
            Math.Abs(p.X - level.PlayerStart.x) < 50 && 
            Math.Abs(p.Y - level.PlayerStart.y) < 50);

        if (startPlatform == null) return false;

        var visited = new HashSet<int>();
        var queue = new Queue<PathNode>();

        var startNode = pathGraph.FirstOrDefault(n => n.PlatformId == startPlatform.Id);
        if (startNode == null) return false;

        queue.Enqueue(startNode);
        visited.Add(startNode.PlatformId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            // Проверить, достигли ли конца
            float distToEnd = CalculateDistance(
                current.X, current.Y, 
                level.PlayerEnd.x, level.PlayerEnd.y);

            if (distToEnd < 100) return true;

            // Исследовать соседние платформы
            foreach (int nextIdx in current.ConnectedPlatforms)
            {
                var nextNode = pathGraph[nextIdx];
                if (!visited.Contains(nextNode.PlatformId))
                {
                    visited.Add(nextNode.PlatformId);
                    queue.Enqueue(nextNode);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Находит тупики в уровне
    /// </summary>
    private List<int> FindDeadEnds(List<PathNode> pathGraph)
    {
        var deadEnds = new List<int>();

        foreach (var node in pathGraph)
        {
            // Если платформа не имеет выхода (кроме вниз)
            if (node.ConnectedPlatforms.Count == 0)
            {
                deadEnds.Add(node.PlatformId);
            }
        }

        return deadEnds;
    }

    /// <summary>
    /// Анализирует кривую сложности через весь уровень
    /// </summary>
    private float AnalyzeDifficultyPath(ProceduralLevelGenerator.GeneratedLevel level, List<PathNode> pathGraph)
    {
        float totalDifficulty = 0;
        int samplePoints = 0;

        // Пройти от начала к концу и собрать данные о сложности
        foreach (var node in pathGraph)
        {
            // Враги рядом = опасно
            int nearbyEnemies = level.Enemies.Count(e => 
                CalculateDistance(node.X, node.Y, e.X, e.Y) < 100);

            // Препятствия рядом = опасно
            int nearbyObstacles = level.Obstacles.Count(o => 
                CalculateDistance(node.X, node.Y, o.X, o.Y) < 80);

            float nodeDifficulty = (nearbyEnemies * 0.3f + nearbyObstacles * 0.2f + node.DangerLevel * 0.5f);
            totalDifficulty += Math.Min(nodeDifficulty, 1f);
            samplePoints++;
        }

        return samplePoints > 0 ? totalDifficulty / samplePoints : 0.5f;
    }

    /// <summary>
    /// Анализирует распределение ресурсов
    /// </summary>
    private float AnalyzeResourceDistribution(ProceduralLevelGenerator.GeneratedLevel level)
    {
        // Идеально 10-20 собираемых предметов
        float collectibleScore = Math.Min(level.Collectibles.Count / 15f, 1f);

        // Ресурсы должны быть распределены по всему уровню
        float minX = level.Collectibles.Min(c => c.X);
        float maxX = level.Collectibles.Max(c => c.X);
        float distribution = (maxX - minX) / level.Width;

        return (collectibleScore * 0.6f + distribution * 0.4f);
    }

    /// <summary>
    /// Анализирует опасные зоны
    /// </summary>
    private List<(int x, int y)> AnalyzeDangerZones(ProceduralLevelGenerator.GeneratedLevel level)
    {
        var dangerZones = new List<(int x, int y)>();

        // Зоны, где враги и препятствия перекрываются
        foreach (var enemy in level.Enemies)
        {
            var nearbyObstacles = level.Obstacles
                .Where(o => CalculateDistance(enemy.X, enemy.Y, o.X, o.Y) < DANGER_RADIUS);

            foreach (var obstacle in nearbyObstacles)
            {
                dangerZones.Add(((enemy.X + obstacle.X) / 2, (enemy.Y + obstacle.Y) / 2));
            }
        }

        return dangerZones;
    }

    /// <summary>
    /// Проверяет равномерность расстояний между платформами
    /// </summary>
    private bool CheckPlatformSpacing(ProceduralLevelGenerator.GeneratedLevel level)
    {
        if (level.Platforms.Count < 2) return true;

        var sortedByY = level.Platforms.OrderBy(p => p.Y).ToList();
        var verticalDistances = new List<int>();

        for (int i = 1; i < sortedByY.Count; i++)
        {
            verticalDistances.Add(sortedByY[i].Y - sortedByY[i - 1].Y);
        }

        // Проверить, есть ли резких скачков
        float avgDistance = (float)verticalDistances.Average();
        bool hasLargeGap = verticalDistances.Any(d => d > avgDistance * 2);

        return !hasLargeGap;
    }

    /// <summary>
    /// Рассчитывает общую игровую ценность
    /// </summary>
    private float CalculatePlayability(ProceduralLevelGenerator.GeneratedLevel level, List<PathNode> pathGraph)
    {
        // Оценка на основе количества платформ, врагов и ресурсов
        float platformScore = Math.Min(level.Platforms.Count / 30f, 1f);
        float enemyBalance = Math.Abs(0.5f - Math.Min(level.Enemies.Count / 10f, 1f)) < 0.2f ? 1f : 0.5f;
        float resourceBalance = Math.Abs(0.5f - AnalyzeResourceDistribution(level)) < 0.2f ? 1f : 0.5f;

        return (platformScore * 0.4f + enemyBalance * 0.3f + resourceBalance * 0.3f);
    }

    /// <summary>
    /// Рассчитывает опасность перехода между платформами
    /// </summary>
    private float CalculateDanger(ProceduralLevelGenerator.GeneratedLevel level, PathNode from, PathNode to)
    {
        // Враги на пути = опасно
        int enemiesOnPath = level.Enemies.Count(e =>
            e.X > Math.Min(from.X, to.X) && e.X < Math.Max(from.X, to.X) &&
            e.Y > Math.Min(from.Y, to.Y) && e.Y < Math.Max(from.Y, to.Y));

        // Препятствия на пути = опасно
        int obstaclesOnPath = level.Obstacles.Count(o =>
            o.X > Math.Min(from.X, to.X) && o.X < Math.Max(from.X, to.X) &&
            o.Y > Math.Min(from.Y, to.Y) && o.Y < Math.Max(from.Y, to.Y));

        float dangerLevel = Math.Min((enemiesOnPath * 0.4f + obstaclesOnPath * 0.3f), 1f);
        return dangerLevel;
    }

    // === Вспомогательные методы ===

    private float CalculateDistance(PathNode n1, PathNode n2) 
        => CalculateDistance(n1.X, n1.Y, n2.X, n2.Y);

    private float CalculateDistance(int x1, int y1, int x2, int y2)
    {
        return (float)Math.Sqrt(
            Math.Pow(x2 - x1, 2) + 
            Math.Pow(y2 - y1, 2));
    }
}
