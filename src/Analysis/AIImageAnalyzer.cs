using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// AI анализатор изображений для обнаружения структуры уровня
/// Использует методы компьютерного зрения для анализа:
/// - Глубины (depth map)
/// - Объектов
/// - Структуры сцены
/// - Интересных точек
/// </summary>
public class AIImageAnalyzer
{
    private const int DEPTH_SAMPLES = 32;
    private const int EDGE_THRESHOLD = 128;

    public class DepthMap
    {
        public float[,] Values { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        
        public float GetDepth(int x, int y) => Values[y, x];
        public float GetAverageDepth() => Values.Cast<float>().Average();
    }

    public class DetectedObject
    {
        public int Id { get; set; }
        public string Type { get; set; } // "platform", "obstacle", "collectible", "surface"
        public string RawClass { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public float Confidence { get; set; }
        public List<(int x, int y)> Boundary { get; set; } = new();
        public float AverageDepth { get; set; }
        public Rgba32 Color { get; set; }
    }

    public class SceneAnalysis
    {
        public DepthMap DepthMap { get; set; }
        public List<DetectedObject> Objects { get; set; } = new();
        public List<(int x, int y)> InterestPoints { get; set; } = new();
        public Dictionary<string, float> SceneProperties { get; set; } = new();
        public float Complexity { get; set; } // 0-1, где 1 = максимальная сложность
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
    }

    /// <summary>
    /// Анализирует изображение и создает карту глубины и объектов
    /// </summary>
    public SceneAnalysis Analyze(string imagePath, DepthMap? externalDepth = null)
    {
        try
        {
            // Попробуем загрузить изображение, поддерживаем различные форматы (PNG, JPG, JFIF, GIF и т.д.)
            Image<Rgba32> image;
            try
            {
                image = Image.Load<Rgba32>(imagePath);
            }
            catch (Exception ex)
            {
                // Если Image.Load не работает, попробуем через generic Image
                using (var genericImage = SixLabors.ImageSharp.Image.Load(imagePath))
                {
                    image = genericImage.CloneAs<Rgba32>();
                }
            }

            using (image)
            {
                var analysis = new SceneAnalysis();
                analysis.ImageWidth = image.Width;
                analysis.ImageHeight = image.Height;

                // Шаг 1: карта глубины — реальная из ИИ-сервиса (Depth Anything V2), если она
                // передана и совпадает по размеру; иначе классическое приближение по яркости.
                analysis.DepthMap = (externalDepth != null
                        && externalDepth.Width == image.Width
                        && externalDepth.Height == image.Height)
                    ? externalDepth
                    : AnalyzeDepth(image);

                // Шаг 2: Обнаружить объекты (на основе цвета и контуров)
                var edgeMap = BuildEdgeMap(image);
                analysis.Objects = DetectObjects(image, analysis.DepthMap, edgeMap);

                // Шаг 3: Найти интересные точки
                analysis.InterestPoints = FindInterestPoints(image);

                // Шаг 4: Рассчитать свойства сцены
                analysis.SceneProperties = AnalyzeSceneProperties(image, analysis);

                // Шаг 5: Определить сложность
                analysis.Complexity = CalculateComplexity(analysis);

                return analysis;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error analyzing image: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Строит карту глубины: яркие области = близко, темные = далеко
    /// </summary>
    private DepthMap AnalyzeDepth(Image<Rgba32> image)
    {
        var depthMap = new DepthMap 
        { 
            Width = image.Width, 
            Height = image.Height,
            Values = new float[image.Height, image.Width]
        };

        // Первый проход: перцептивная яркость (eye-weighted) → глубина. Точнее, чем среднее RGB,
        // т.к. учитывает реальную чувствительность глаза к зелёному/красному/синему.
        float min = 1f, max = 0f;
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                float lum = (0.299f * pixel.R + 0.587f * pixel.G + 0.114f * pixel.B) / 255f;
                depthMap.Values[y, x] = lum;
                if (lum < min) min = lum;
                if (lum > max) max = lum;
            }
        }

        // Второй проход: растяжение контраста под диапазон конкретного кадра, чтобы рельеф
        // использовал всю высоту, а не сжимался в узкую полосу (точнее по каждому фото).
        float range = MathF.Max(1e-4f, max - min);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                depthMap.Values[y, x] = (depthMap.Values[y, x] - min) / range;
            }
        }

        // Третий проход: размытие для гладкой карты глубины
        ApplyGaussianBlur(depthMap.Values);

        return depthMap;
    }

    /// <summary>
    /// Обнаруживает объекты по цвету и форме
    /// </summary>
    private List<DetectedObject> DetectObjects(Image<Rgba32> image, DepthMap depthMap, byte[,] edgeMap)
    {
        var objects = new List<DetectedObject>();
        var visited = new bool[image.Height, image.Width];
        int objectId = 0;
        int minArea = Math.Max(60, (int)(image.Width * image.Height * 0.0003f));
        int minBoundary = 24;

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (!visited[y, x])
                {
                    var obj = DetectObjectAtPoint(image, depthMap, edgeMap, x, y, visited, objectId++);
                    int area = obj == null ? 0 : obj.Width * obj.Height;
                    if (obj != null && obj.Boundary.Count > minBoundary && area >= minArea) // Минимальный размер объекта
                    {
                        objects.Add(obj);
                    }
                }
            }
        }

        return objects;
    }

    /// <summary>
    /// Обнаруживает один объект методом заливки (flood fill)
    /// </summary>
    private DetectedObject DetectObjectAtPoint(Image<Rgba32> image, DepthMap depthMap, byte[,] edgeMap,
        int startX, int startY, bool[,] visited, int objectId)
    {
        var pixel = image[startX, startY];
        var boundary = new List<(int x, int y)>();
        var queue = new Queue<(int x, int y)>();

        queue.Enqueue((startX, startY));
        visited[startY, startX] = true;

        // Flood fill для определения границ объекта
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            boundary.Add((x, y));

            // Проверяем соседние пиксели
            foreach (var (nx, ny) in GetNeighbors(x, y, image.Width, image.Height))
            {
                if (!visited[ny, nx])
                {
                    var neighborPixel = image[nx, ny];
                    if (edgeMap[ny, nx] < EDGE_THRESHOLD && IsSimilarColor(pixel, neighborPixel))
                    {
                        visited[ny, nx] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }
        }

        // Рассчитать bounding box
        int minX = boundary.Min(p => p.x);
        int maxX = boundary.Max(p => p.x);
        int minY = boundary.Min(p => p.y);
        int maxY = boundary.Max(p => p.y);

        int width = maxX - minX;
        int height = maxY - minY;
        float avgDepth = boundary.Average(p => depthMap.GetDepth(p.x, p.y));

        // Средний цвет ВСЕЙ области (а не одного семенного пикселя) — стабильнее и точнее
        // для классификации и для отрисовки объекта в 3D.
        long sumR = 0, sumG = 0, sumB = 0;
        foreach (var (bx, by) in boundary)
        {
            var bp = image[bx, by];
            sumR += bp.R; sumG += bp.G; sumB += bp.B;
        }
        int regionCount = boundary.Count;
        var avgColor = new Rgba32(
            (byte)(sumR / regionCount),
            (byte)(sumG / regionCount),
            (byte)(sumB / regionCount));

        // Нормализованные характеристики для классификатора (он ожидает шкалу 0..1).
        float r = avgColor.R / 255f;
        float g = avgColor.G / 255f;
        float b = avgColor.B / 255f;
        float brightness = (avgColor.R + avgColor.G + avgColor.B) / 3f / 255f; // 0..1
        float contrast = CalculateContrastForObject(boundary, image, minX, minY, maxX, maxY); // 0..1

        string objectType = AdvancedObjectClassifier.ClassifyObjectAdvanced(
            r, g, b,
            width, height, minX, minY,
            avgDepth, brightness, contrast,
            image.Width, image.Height,
            boundary
        );

        // Уверенность зависит от того, насколько плотно область заполняет свой bounding box:
        // компактные, чётко очерченные регионы достовернее, чем «рваные» заливки фона.
        float bboxArea = Math.Max(1f, (width + 1f) * (height + 1f));
        float fillRatio = Math.Clamp(regionCount / bboxArea, 0f, 1f);
        float confidence = Math.Clamp(0.45f + 0.5f * fillRatio, 0.45f, 0.95f);

        return new DetectedObject
        {
            Id = objectId,
            Type = objectType,
            RawClass = objectType,
            X = minX,
            Y = minY,
            Width = width,
            Height = height,
            Boundary = boundary,
            Confidence = confidence,
            AverageDepth = avgDepth,
            Color = avgColor
        };
    }

    /// <summary>
    /// Рассчитывает контраст объекта с его реальным окружением (0..1).
    /// Сравнивает среднюю яркость области со средней яркостью «кольца» пикселей сразу
    /// за пределами bounding box — это даёт осмысленный контраст вместо догадки о фоне.
    /// </summary>
    private float CalculateContrastForObject(List<(int x, int y)> region, Image<Rgba32> image,
        int minX, int minY, int maxX, int maxY)
    {
        if (region.Count < 4)
            return 0;

        float inside = region
            .Select(p => GetBrightness(image[p.x, p.y]))
            .Average(); // 0..255

        // Кольцо вокруг bounding box (на несколько пикселей шире), обрезанное по краям картинки.
        const int pad = 3;
        int x0 = Math.Max(0, minX - pad), x1 = Math.Min(image.Width - 1, maxX + pad);
        int y0 = Math.Max(0, minY - pad), y1 = Math.Min(image.Height - 1, maxY + pad);

        double sum = 0;
        int cnt = 0;
        for (int x = x0; x <= x1; x++)
        {
            sum += GetBrightness(image[x, y0]); cnt++;
            sum += GetBrightness(image[x, y1]); cnt++;
        }
        for (int y = y0; y <= y1; y++)
        {
            sum += GetBrightness(image[x0, y]); cnt++;
            sum += GetBrightness(image[x1, y]); cnt++;
        }

        float outside = cnt > 0 ? (float)(sum / cnt) : inside;
        return Math.Clamp(MathF.Abs(inside - outside) / 255f, 0f, 1f); // 0..1
    }

    /// <summary>
    /// Классифицирует объект по цвету (старый метод - оставлен для совместимости)
    /// </summary>
    private string ClassifyObject(Rgba32 pixel)
    {
        float r = pixel.R / 255f;
        float g = pixel.G / 255f;
        float b = pixel.B / 255f;

        if (r > 0.6 && g < 0.3 && b < 0.3) return "obstacle";      // Красный
        if (g > 0.6 && r < 0.3 && b < 0.3) return "collectible";   // Зелёный
        if (b > 0.6 && r < 0.3 && g < 0.3) return "moving_platform"; // Синий
        if (r + g + b > 2.0) return "platform";                      // Белый/яркий
        if (r + g + b < 0.5) return "shadow";                        // Чёрный

        return "surface";
    }

    /// <summary>
    /// Находит интересные точки (углы, центры объектов)
    /// </summary>
    private List<(int x, int y)> FindInterestPoints(Image<Rgba32> image)
    {
        var interestPoints = new List<(int x, int y)>();
        
        // Ищем точки высокого контраста (корнер-детектор)
        for (int y = 1; y < image.Height - 1; y++)
        {
            for (int x = 1; x < image.Width - 1; x++)
            {
                float contrast = CalculateContrast(image, x, y);
                if (contrast > 0.3f)
                {
                    interestPoints.Add((x, y));
                }
            }
        }

        // Уменьшить количество точек (sampling)
        return interestPoints.Where((_, i) => i % 5 == 0).ToList();
    }

    /// <summary>
    /// Рассчитывает контраст вокруг пиксела (краевой детектор)
    /// </summary>
    private float CalculateContrast(Image<Rgba32> image, int x, int y)
    {
        float center = GetBrightness(image[x, y]);
        float totalDiff = 0;
        float count = 0;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx != 0 || dy != 0)
                {
                    float neighbor = GetBrightness(image[x + dx, y + dy]);
                    totalDiff += Math.Abs(center - neighbor);
                    count++;
                }
            }
        }

        return totalDiff / count / 255f;
    }

    /// <summary>
    /// Анализирует общие свойства сцены
    /// </summary>
    private Dictionary<string, float> AnalyzeSceneProperties(Image<Rgba32> image, SceneAnalysis analysis)
    {
        var properties = new Dictionary<string, float>();

        // Средняя яркость
        float totalBrightness = 0;
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                totalBrightness += GetBrightness(image[x, y]);
            }
        }
        properties["average_brightness"] = totalBrightness / (image.Width * image.Height) / 255f;

        // Количество объектов каждого типа
        properties["obstacle_count"] = analysis.Objects.Count(o => o.Type == "obstacle");
        properties["platform_count"] = analysis.Objects.Count(o => o.Type == "platform");
        properties["collectible_count"] = analysis.Objects.Count(o => o.Type == "collectible");

        // Средняя глубина
        properties["average_depth"] = analysis.DepthMap.GetAverageDepth();

        return properties;
    }

    /// <summary>
    /// Рассчитывает сложность уровня (0-1)
    /// </summary>
    private float CalculateComplexity(SceneAnalysis analysis)
    {
        float objectDensity = Math.Min(analysis.Objects.Count / 100f, 1f);
        float depthVariation = CalculateDepthVariation(analysis.DepthMap);
        
        // Взвешенная комбинация
        return (objectDensity * 0.5f + depthVariation * 0.5f);
    }

    /// <summary>
    /// Рассчитывает вариацию глубины
    /// </summary>
    private float CalculateDepthVariation(DepthMap depthMap)
    {
        float avg = depthMap.GetAverageDepth();
        float variance = 0;
        int count = 0;

        for (int y = 0; y < depthMap.Height; y++)
        {
            for (int x = 0; x < depthMap.Width; x++)
            {
                variance += Math.Abs(depthMap.Values[y, x] - avg);
                count++;
            }
        }

        return Math.Min(variance / count, 1f);
    }

    // === Вспомогательные методы ===

    private float GetBrightness(Rgba32 pixel) => (pixel.R + pixel.G + pixel.B) / 3f;

    private bool IsSimilarColor(Rgba32 pixel1, Rgba32 pixel2, int threshold = 40)
    {
        return Math.Abs(pixel1.R - pixel2.R) < threshold &&
               Math.Abs(pixel1.G - pixel2.G) < threshold &&
               Math.Abs(pixel1.B - pixel2.B) < threshold;
    }

    private IEnumerable<(int x, int y)> GetNeighbors(int x, int y, int width, int height)
    {
        if (x > 0) yield return (x - 1, y);
        if (x < width - 1) yield return (x + 1, y);
        if (y > 0) yield return (x, y - 1);
        if (y < height - 1) yield return (x, y + 1);
    }

    private void ApplyGaussianBlur(float[,] data)
    {
        int height = data.GetLength(0);
        int width = data.GetLength(1);
        var blurred = new float[height, width];

        float[,] kernel = new float[,] { { 0.05f, 0.15f, 0.05f }, 
                                          { 0.15f, 0.4f, 0.15f }, 
                                          { 0.05f, 0.15f, 0.05f } };

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                float sum = 0;
                for (int ky = -1; ky <= 1; ky++)
                {
                    for (int kx = -1; kx <= 1; kx++)
                    {
                        sum += data[y + ky, x + kx] * kernel[ky + 1, kx + 1];
                    }
                }
                blurred[y, x] = sum;
            }
        }

        // Скопировать обратно
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                data[y, x] = blurred[y, x];
            }
        }
    }

    private byte[,] BuildEdgeMap(Image<Rgba32> image)
    {
        var edgeMap = new byte[image.Height, image.Width];

        for (int y = 1; y < image.Height - 1; y++)
        {
            for (int x = 1; x < image.Width - 1; x++)
            {
                float tl = GetBrightness(image[x - 1, y - 1]);
                float tc = GetBrightness(image[x, y - 1]);
                float tr = GetBrightness(image[x + 1, y - 1]);
                float ml = GetBrightness(image[x - 1, y]);
                float mr = GetBrightness(image[x + 1, y]);
                float bl = GetBrightness(image[x - 1, y + 1]);
                float bc = GetBrightness(image[x, y + 1]);
                float br = GetBrightness(image[x + 1, y + 1]);

                float gx = (tr + 2 * mr + br) - (tl + 2 * ml + bl);
                float gy = (bl + 2 * bc + br) - (tl + 2 * tc + tr);
                float magnitude = MathF.Sqrt(gx * gx + gy * gy);
                edgeMap[y, x] = (byte)Math.Clamp(magnitude, 0f, 255f);
            }
        }

        return edgeMap;
    }
}
