using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// УЛУЧШЕННЫЙ АНАЛИЗАТОР - Advanced AI для лучшего различения объектов
/// Использует multi-factor анализ:
/// - Размер и пропорции объекта
/// - Положение в пространстве (Y координата)
/// - Цвет и контраст
/// - Текстура и детали
/// - Глубина и перспектива
/// </summary>
public class AdvancedObjectClassifier
{
    /// <summary>
    /// Классифицирует объект используя множество критериев
    /// </summary>
    public static string ClassifyObjectAdvanced(
        float red, float green, float blue,
        int width, int height, int xPos, int yPos,
        float avgDepth, float brightness, float contrast,
        float sceneWidth, float sceneHeight,
        List<(int x, int y)> boundary)
    {
        // Вычисляем различные метрики
        float aspectRatio = width > 0 ? (float)height / width : 1f;
        float area = width * height;
        float areaRatio = area / (sceneWidth * sceneHeight);
        float verticalPosition = yPos / sceneHeight;  // 0.0 = top, 1.0 = bottom
        float horizontalCenter = (xPos + width / 2f) / sceneWidth;
        
        // Вычисляем цветовые характеристики
        float hue = GetHue(red, green, blue);
        float saturation = GetSaturation(red, green, blue);
        float luminance = (0.299f * red + 0.587f * green + 0.114f * blue);
        
        // Вычисляем форму (roundness)
        float roundness = CalculateRoundness(boundary, width, height);
        
        // Вычисляем текстуру (если есть мелкие детали)
        float detailDensity = CalculateDetailDensity(boundary);

        // ====== ЛОГИКА КЛАССИФИКАЦИИ ======
        
        // 1. ПЛАТФОРМЫ - обычно горизонтальные, в нижней половине
        if (IsLikelyPlatform(width, height, aspectRatio, verticalPosition, 
                           saturation, luminance, areaRatio, detailDensity))
        {
            return "platform";
        }

        // 2. ПРЕПЯТСТВИЯ - кубики, объекты среднего размера, часто высокий контраст
        if (IsLikelyObstacle(width, height, aspectRatio, contrast, 
                           red, green, blue, areaRatio, roundness))
        {
            return "obstacle";
        }

        // 3. ВРАГИ - средние объекты, часто движущиеся (темные или яркие)
        if (IsLikelyEnemy(width, height, aspectRatio, verticalPosition,
                        luminance, saturation, brightness, areaRatio))
        {
            return "enemy";
        }

        // 4. КОЛЛЕКТИБЛИ - маленькие, яркие, часто желтые/золотые
        if (IsLikelyCollectible(width, height, areaRatio,
                              hue, saturation, luminance, brightness))
        {
            return "collectible";
        }

        // 5. ДВИЖУЩИЕСЯ ПЛАТФОРМЫ - платформы, но меньше или с другим цветом
        if (IsLikelyMovingPlatform(width, height, aspectRatio, saturation,
                                 brightness, areaRatio, verticalPosition))
        {
            return "moving_platform";
        }

        // 6. ТЕНЬ - темные области
        if (luminance < 0.2f && saturation < 0.1f)
        {
            return "shadow";
        }

        // 7. ПОВЕРХНОСТЬ - по умолчанию
        return "surface";
    }

    // ====== КЛАССИФИКАТОРЫ ======

    private static bool IsLikelyPlatform(float width, float height, float aspectRatio,
                                       float verticalPos, float saturation, float luminance,
                                       float areaRatio, float detailDensity)
    {
        // Платформы:
        // - Широкие и плоские (width > height, aspect ratio > 1.5)
        // - В нижней половине сцены (verticalPos > 0.4)
        // - Часто светлые (luminance > 0.4)
        // - Низкий уровень детализации
        // - Средний/большой размер

        bool isWideShape = aspectRatio > 1.5f && aspectRatio < 10f;
        bool isInLowerHalf = verticalPos > 0.35f;
        bool isBright = luminance > 0.35f;
        bool isNotTooSmall = areaRatio > 0.01f;
        bool hasLowDetail = detailDensity < 0.3f;

        return isWideShape && isInLowerHalf && isBright && isNotTooSmall && hasLowDetail;
    }

    private static bool IsLikelyObstacle(float width, float height, float aspectRatio,
                                       float contrast, float r, float g, float b,
                                       float areaRatio, float roundness)
    {
        // Препятствия:
        // - Примерно кубические (aspct ratio близко к 1)
        // - Часто темные или красные
        // - Высокий контраст с фоном
        // - Не слишком малые и не слишком большие

        bool isCompact = aspectRatio > 0.4f && aspectRatio < 3f;
        bool isReddish = r > 0.5f && g < 0.5f && b < 0.5f;
        bool hasHighContrast = contrast > 0.3f;
        bool isProperSize = areaRatio > 0.002f && areaRatio < 0.2f;
        bool isNotRound = roundness < 0.6f;

        return isCompact && (isReddish || hasHighContrast) && isProperSize && 
               isNotRound;
    }

    private static bool IsLikelyEnemy(float width, float height, float aspectRatio,
                                    float verticalPos, float luminance, float saturation,
                                    float brightness, float areaRatio)
    {
        // Враги:
        // - Примерно кубические или чуть вытянутые
        // - В верхней/средней части (verticalPos < 0.8)
        // - Не очень большие
        // - Часто окрашены (средняя насыщенность)

        bool isCompact = aspectRatio > 0.3f && aspectRatio < 2f;
        bool isNotTooLow = verticalPos < 0.8f;
        bool isModerateSize = areaRatio > 0.0005f && areaRatio < 0.1f;
        bool hasModerateColor = saturation > 0.2f;

        return isCompact && isNotTooLow && isModerateSize && hasModerateColor;
    }

    private static bool IsLikelyCollectible(float width, float height, float areaRatio,
                                          float hue, float saturation, float luminance,
                                          float brightness)
    {
        // Коллектибли:
        // - МАЛЕНЬКИЕ 
        // - Часто желтые/золотые (hue ~60°) или яркие
        // - Высокая яркость
        // - Средне насыщенные

        bool isSmall = areaRatio < 0.005f;
        bool isYellowish = (hue > 40f && hue < 80f) || (luminance > 0.6f && brightness > 0.7f);
        bool isBright = luminance > 0.5f || brightness > 0.6f;
        bool hasColorSaturation = saturation > 0.3f || luminance > 0.7f;

        return isSmall && (isYellowish || isBright) && hasColorSaturation;
    }

    private static bool IsLikelyMovingPlatform(float width, float height, float aspectRatio,
                                             float saturation, float brightness,
                                             float areaRatio, float verticalPos)
    {
        // Движущиеся платформы:
        // - Платформы, но проще (меньше высокая насыщенность)
        // - Часто синие или другого цвета
        // - Среднего размера

        bool isWideEnough = aspectRatio > 1.2f && aspectRatio < 8f;
        bool isModerateSize = areaRatio > 0.005f && areaRatio < 0.15f;
        bool hasColorDifference = saturation > 0.4f;
        bool isNotTopArea = verticalPos > 0.2f;

        return isWideEnough && isModerateSize && hasColorDifference && isNotTopArea;
    }

    // ====== ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ======

    /// <summary>
    /// Рассчитывает Hue (оттенок) из RGB (0-360 градусов)
    /// </summary>
    private static float GetHue(float r, float g, float b)
    {
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float delta = max - min;

        if (delta == 0)
            return 0;

        float hue = 0;
        if (max == r)
            hue = (g - b) / delta;
        else if (max == g)
            hue = 2 + (b - r) / delta;
        else
            hue = 4 + (r - g) / delta;

        hue *= 60;
        if (hue < 0)
            hue += 360;

        return hue;
    }

    /// <summary>
    /// Рассчитывает Saturation (насыщенность) из RGB (0-1)
    /// </summary>
    private static float GetSaturation(float r, float g, float b)
    {
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float luminance = (max + min) / 2;

        if (max == min)
            return 0;

        float saturation = (max - min) / (1 - MathF.Abs(2 * luminance - 1));
        return saturation;
    }

    /// <summary>
    /// Рассчитывает roundness (округлость объекта 0-1)
    /// </summary>
    private static float CalculateRoundness(List<(int x, int y)> boundary, 
                                          float width, float height)
    {
        if (boundary.Count < 4)
            return 0;

        // Периметр примерно
        float perimeter = boundary.Count;
        
        // Область регулярного прямоугольника
        float expectedPerimeterRect = 2 * (width + height);
        
        // Если периметр близко к квадрату, это прямоугольник
        // Если форма округлая, периметр выше
        return MathF.Min(1, perimeter / expectedPerimeterRect);
    }

    /// <summary>
    /// Рассчитывает плотность деталей (текстуру)
    /// </summary>
    private static float CalculateDetailDensity(List<(int x, int y)> boundary)
    {
        if (boundary.Count < 4)
            return 0;

        // Считаем перемены направления в границе
        int directionChanges = 0;
        int prevDir = 0;

        for (int i = 1; i < boundary.Count - 1; i++)
        {
            var (x1, y1) = boundary[i - 1];
            var (x2, y2) = boundary[i];
            var (x3, y3) = boundary[i + 1];

            int dx1 = x2 - x1;
            int dy1 = y2 - y1;
            int dx2 = x3 - x2;
            int dy2 = y3 - y2;

            int currDir = (dx2 * dy1 - dx1 * dy2) > 0 ? 1 : -1;
            if (currDir != prevDir)
                directionChanges++;

            prevDir = currDir;
        }

        // Нормализовать: больше изменений = более сложная текстура
        return MathF.Min(1, directionChanges / (float)boundary.Count);
    }
}

/// <summary>
/// Enhanced Scene Properties Analyzer
/// </summary>
public static class EnhancedSceneAnalyzer
{
    public static Dictionary<string, float> AnalyzeSceneQuality(
        float complexity, int objectCount, int platformCount,
        float avgBrightness, float avgContrast)
    {
        var properties = new Dictionary<string, float>();

        // Определяем качество анализа
        properties["scene_analysis_quality"] = MathF.Min(1, 
            (objectCount * 0.01f) + (platformCount * 0.02f) + (complexity * 0.5f));

        // Сложность сцены
        properties["scene_complexity_score"] = complexity;

        // Объектность сцены (сколько объектов найдено)
        properties["object_density"] = MathF.Min(1, objectCount / 100f);

        // Контрастность (для лучшей различимости)
        properties["contrast_quality"] = avgContrast;

        // Яркость
        properties["brightness_score"] = avgBrightness;

        // Рекомендация по качеству ввода
        if (complexity > 0.7 && platformCount > 5 && objectCount > 30)
            properties["recommended_difficulty"] = 3f; // Сложный уровень

        else if (complexity > 0.4 && platformCount > 3 && objectCount > 15)
            properties["recommended_difficulty"] = 2f; // Средний уровень

        else
            properties["recommended_difficulty"] = 1f; // Лёгкий уровень

        return properties;
    }
}
