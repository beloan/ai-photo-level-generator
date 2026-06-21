using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Enhanced Texture Manager - Улучшенное управление текстурами
/// с повышенной реальности на основе анализа изображения
/// </summary>
public class EnhancedTextureManager
{
    /// <summary>
    /// Материал с расширенными свойствами
    /// </summary>
    public class AdvancedMaterial
    {
        public string Name { get; set; }
        public (byte R, byte G, byte B) BaseColor { get; set; }
        public (byte R, byte G, byte B) SecondaryColor { get; set; }
        public float Metalness { get; set; } // 0-1
        public float Roughness { get; set; } // 0-1
        public float Emit { get; set; } // 0-1 для излучающих объектов
        public string PatternType { get; set; } // "smooth", "metallic", "rough", "matte", "glossy"
        
        public override string ToString()
        {
            return $"{Name} (Base: RGB{BaseColor}, Metal: {Metalness:F2}, Rough: {Roughness:F2})";
        }
    }

    /// <summary>
    /// Создает материал на основе типа объекта и его свойств
    /// </summary>
    public static AdvancedMaterial CreateMaterialForObjectType(
        string objectType, 
        float brightness, 
        float saturation,
        float hue)
    {
        var material = new AdvancedMaterial { Name = objectType };

        // Преобразовать HSB в RGB
        var baseColor = HSBToRGB(hue, saturation, brightness);
        material.BaseColor = baseColor;

        // Создать комплементарный цвет
        material.SecondaryColor = HSBToRGB((hue + 180) % 360, saturation * 0.6f, brightness * 0.7f);

        // Применить свойства на основе типа
        return objectType switch
        {
            // ==== ПЛАТФОРМЫ ====
            "platform" => new AdvancedMaterial
            {
                Name = "platform",
                BaseColor = (120, 120, 120),
                SecondaryColor = (80, 80, 80),
                Metalness = 0.3f,
                Roughness = 0.6f,
                PatternType = "matte"
            },

            "moving_platform" => new AdvancedMaterial
            {
                Name = "moving_platform",
                BaseColor = (100, 150, 200),
                SecondaryColor = (60, 100, 150),
                Metalness = 0.6f,
                Roughness = 0.4f,
                PatternType = "metallic"
            },

            // ==== ВРАГИ ====
            "enemy" => new AdvancedMaterial
            {
                Name = "enemy",
                BaseColor = (200, 50, 50),
                SecondaryColor = (100, 20, 20),
                Metalness = 0.4f,
                Roughness = 0.7f,
                Emit = 0.1f,
                PatternType = "rough"
            },

            // ==== КОЛЛЕКТИБЛИ ====
            "collectible" => new AdvancedMaterial
            {
                Name = "collectible",
                BaseColor = (255, 215, 0),
                SecondaryColor = (255, 255, 150),
                Metalness = 0.9f,
                Roughness = 0.2f,
                Emit = 0.3f,
                PatternType = "glossy"
            },

            // ==== ПРЕПЯТСТВИЯ ====
            "obstacle" => new AdvancedMaterial
            {
                Name = "obstacle",
                BaseColor = (150, 100, 50),
                SecondaryColor = (100, 60, 30),
                Metalness = 0.2f,
                Roughness = 0.8f,
                PatternType = "rough"
            },

            // ==== ТЕНИ ====
            "shadow" => new AdvancedMaterial
            {
                Name = "shadow",
                BaseColor = (30, 30, 30),
                SecondaryColor = (10, 10, 10),
                Metalness = 0.0f,
                Roughness = 1.0f,
                PatternType = "matte"
            },

            // === DEFAULT ===
            _ => new AdvancedMaterial
            {
                Name = objectType,
                BaseColor = baseColor,
                SecondaryColor = material.SecondaryColor,
                Metalness = 0.4f,
                Roughness = 0.6f,
                PatternType = "matte"
            }
        };
    }

    /// <summary>
    /// Преобразует HSB в RGB
    /// </summary>
    private static (byte R, byte G, byte B) HSBToRGB(float hue, float saturation, float brightness)
    {
        float c = brightness * saturation;
        float hh = hue / 60f;
        float x = c * (1 - Math.Abs((hh % 2) - 1));

        float r, g, b;
        
        if (hh < 1)
            (r, g, b) = (c, x, 0);
        else if (hh < 2)
            (r, g, b) = (x, c, 0);
        else if (hh < 3)
            (r, g, b) = (0, c, x);
        else if (hh < 4)
            (r, g, b) = (0, x, c);
        else if (hh < 5)
            (r, g, b) = (x, 0, c);
        else
            (r, g, b) = (c, 0, x);

        float m = brightness - c;
        return (
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255)
        );
    }

    /// <summary>
    /// Преобразует RGB в HSB
    /// </summary>
    public static (float hue, float saturation, float brightness) RGBToHSB(byte r, byte g, byte b)
    {
        float rf = r / 255f;
        float gf = g / 255f;
        float bf = b / 255f;

        float max = MathF.Max(rf, MathF.Max(gf, bf));
        float min = MathF.Min(rf, MathF.Min(gf, bf));
        float delta = max - min;

        float brightness = max;

        float saturation = max == 0 ? 0 : delta / max;

        float hue = 0;
        if (delta != 0)
        {
            hue = max == rf ? (gf - bf) / delta :
                  max == gf ? 2 + (bf - rf) / delta :
                  4 + (rf - gf) / delta;
            hue *= 60;
            if (hue < 0) hue += 360;
        }

        return (hue, saturation, brightness);
    }

    /// <summary>
    /// Создает градиент цветов для визуализации
    /// </summary>
    public static List<(byte R, byte G, byte B)> CreateColorGradient(
        (byte R, byte G, byte B) startColor,
        (byte R, byte G, byte B) endColor,
        int steps = 10)
    {
        var gradient = new List<(byte, byte, byte)>();

        for (int i = 0; i < steps; i++)
        {
            float t = i / (float)(steps - 1);
            byte r = (byte)(startColor.R + (endColor.R - startColor.R) * t);
            byte g = (byte)(startColor.G + (endColor.G - startColor.G) * t);
            byte b = (byte)(startColor.B + (endColor.B - startColor.B) * t);
            gradient.Add((r, g, b));
        }

        return gradient;
    }

    /// <summary>
    /// Применяет паттерн текстуры к материалу
    /// </summary>
    public static string GenerateTexturePattern(string patternType, int width, int height)
    {
        return patternType switch
        {
            "metallic" => $"metallic_{width}x{height}.png",
            "rough" => $"rough_{width}x{height}.png",
            "matte" => $"matte_{width}x{height}.png",
            "glossy" => $"glossy_{width}x{height}.png",
            _ => $"default_{width}x{height}.png"
        };
    }

    /// <summary>
    /// Оптимизирует материалы для 3D рендеринга
    /// </summary>
    public static List<AdvancedMaterial> OptimizeMaterialsForRendering(
        List<AIImageAnalyzer.DetectedObject> detectedObjects)
    {
        var materials = new List<AdvancedMaterial>();
        var materialCache = new Dictionary<string, AdvancedMaterial>();

        foreach (var obj in detectedObjects)
        {
            if (!materialCache.ContainsKey(obj.Type))
            {
                var material = CreateMaterialForObjectType(
                    obj.Type,
                    obj.AverageDepth,
                    0.5f,  // Средняя насыщенность
                    (obj.Id * 30) % 360  // Разные оттенки для разных объектов
                );
                materialCache[obj.Type] = material;
                materials.Add(material);
            }
        }

        return materials;
    }
}
