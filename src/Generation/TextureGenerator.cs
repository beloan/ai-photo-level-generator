using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

/// <summary>
/// Генератор процедурных текстур и материалов
/// Создаёт разнообразные материалы для разных типов объектов
/// </summary>
public class TextureGenerator
{
    /// <summary>
    /// RGB цвет
    /// </summary>
    public struct Color
    {
        public byte R, G, B;

        public Color(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public override string ToString() => $"{R},{G},{B}";
    }

    /// <summary>
    /// Материал в формате MTL
    /// </summary>
    public class Material
    {
        public string Name { get; set; }
        public Color Diffuse { get; set; }
        public Color Specular { get; set; }
        public float Shininess { get; set; } = 32;
        public float Opacity { get; set; } = 1.0f;
        public string TextureFile { get; set; } = "";

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"newmtl {Name}");
            sb.AppendLine($"Ka 0.2 0.2 0.2");
            sb.AppendLine($"Kd {Diffuse.R / 255f:F3} {Diffuse.G / 255f:F3} {Diffuse.B / 255f:F3}");
            sb.AppendLine($"Ks {Specular.R / 255f:F3} {Specular.G / 255f:F3} {Specular.B / 255f:F3}");
            sb.AppendLine($"Ns {Shininess:F1}");
            if (Opacity < 1.0f) sb.AppendLine($"d {Opacity:F3}");
            if (!string.IsNullOrEmpty(TextureFile)) sb.AppendLine($"map_Kd {TextureFile}");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Простая реализация Perlin noise для текстурирования
    /// </summary>
    private static class PerlinNoise
    {
        private static readonly int[] Permutation = new int[512];
        private static readonly Random _rand = new Random(42); // Фиксированный seed для воспроизводимости

        static PerlinNoise()
        {
            var p = Enumerable.Range(0, 256).OrderBy(_ => _rand.Next()).ToArray();
            for (int i = 0; i < 512; i++)
                Permutation[i] = p[i % 256];
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static float Lerp(float t, float a, float b) => a + t * (b - a);

        private static float Grad(int hash, float x, float y)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 8 ? y : x;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        public static float Noise(float x, float y)
        {
            int xi = (int)Math.Floor(x) & 255;
            int yi = (int)Math.Floor(y) & 255;

            float xf = x - (float)Math.Floor(x);
            float yf = y - (float)Math.Floor(y);

            float u = Fade(xf);
            float v = Fade(yf);

            int aa = Permutation[Permutation[xi] + yi];
            int ab = Permutation[Permutation[xi] + yi + 1];
            int ba = Permutation[Permutation[xi + 1] + yi];
            int bb = Permutation[Permutation[xi + 1] + yi + 1];

            float x1 = Lerp(u, Grad(aa, xf, yf), Grad(ba, xf - 1, yf));
            float x2 = Lerp(u, Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1));

            return Lerp(v, x1, x2);
        }
    }

    /// <summary>
    /// Генерирует случайный цвет с вариацией
    /// </summary>
    public static Color GenerateRandomColor(Color baseColor, int variance = 30)
    {
        var rand = new Random();
        byte r = (byte)Math.Clamp(baseColor.R + rand.Next(-variance, variance), 0, 255);
        byte g = (byte)Math.Clamp(baseColor.G + rand.Next(-variance, variance), 0, 255);
        byte b = (byte)Math.Clamp(baseColor.B + rand.Next(-variance, variance), 0, 255);
        return new Color(r, g, b);
    }

    /// <summary>
    /// Генерирует материал для платформы
    /// </summary>
    public static Material CreatePlatformMaterial(string type)
    {
        return type switch
        {
            "moving" => new Material
            {
                Name = "moving_platform",
                Diffuse = new Color(100, 150, 200),       // Светло-синий
                Specular = new Color(200, 200, 200),
                Shininess = 64
            },
            "bouncy" => new Material
            {
                Name = "bouncy_platform",
                Diffuse = new Color(255, 150, 0),         // Оранжевый
                Specular = new Color(255, 200, 100),
                Shininess = 96
            },
            "fragile" => new Material
            {
                Name = "fragile_platform",
                Diffuse = new Color(200, 100, 100),       // Светло-красный
                Specular = new Color(150, 150, 150),
                Shininess = 32
            },
            _ => new Material
            {
                Name = "platform",
                Diffuse = new Color(120, 120, 120),       // Серый
                Specular = new Color(180, 180, 180),
                Shininess = 64
            }
        };
    }

    /// <summary>
    /// Генерирует материал для врага
    /// </summary>
    public static Material CreateEnemyMaterial(string type)
    {
        return type switch
        {
            "flyer" => new Material
            {
                Name = "flyer_enemy",
                Diffuse = new Color(200, 50, 50),         // Красный
                Specular = new Color(255, 100, 100),
                Shininess = 128
            },
            "turret" => new Material
            {
                Name = "turret_enemy",
                Diffuse = new Color(100, 100, 100),       // Серый метал
                Specular = new Color(200, 200, 200),
                Shininess = 128
            },
            _ => new Material
            {
                Name = "walker_enemy",
                Diffuse = new Color(150, 50, 150),        // Фиолетовый
                Specular = new Color(200, 100, 200),
                Shininess = 96
            }
        };
    }

    /// <summary>
    /// Генерирует материал для собираемого предмета
    /// </summary>
    public static Material CreateCollectibleMaterial(string type)
    {
        return type switch
        {
            "crystal" => new Material
            {
                Name = "crystal_material",
                Diffuse = new Color(0, 255, 200),         // Голубой
                Specular = new Color(255, 255, 255),
                Shininess = 200,
                Opacity = 0.8f
            },
            "powerup" => new Material
            {
                Name = "powerup_material",
                Diffuse = new Color(255, 255, 0),         // Жёлтый
                Specular = new Color(255, 255, 200),
                Shininess = 200
            },
            _ => new Material
            {
                Name = "coin_material",
                Diffuse = new Color(255, 200, 0),         // Золотой
                Specular = new Color(255, 255, 150),
                Shininess = 200
            }
        };
    }

    /// <summary>
    /// Генерирует материал для препятствия
    /// </summary>
    public static Material CreateObstacleMaterial(string type)
    {
        return type switch
        {
            "spike" => new Material
            {
                Name = "spike_material",
                Diffuse = new Color(100, 50, 0),          // Коричневый
                Specular = new Color(150, 100, 50),
                Shininess = 64
            },
            "fire" => new Material
            {
                Name = "fire_material",
                Diffuse = new Color(255, 100, 0),         // Огненный оранжевый
                Specular = new Color(255, 200, 100),
                Shininess = 128,
                Opacity = 0.9f
            },
            "acid" => new Material
            {
                Name = "acid_material",
                Diffuse = new Color(50, 200, 50),         // Ядовито-зелёный
                Specular = new Color(100, 255, 100),
                Shininess = 96,
                Opacity = 0.8f
            },
            _ => new Material
            {
                Name = "obstacle_material",
                Diffuse = new Color(80, 80, 80),          // Тёмный серый
                Specular = new Color(150, 150, 150),
                Shininess = 64
            }
        };
    }

    /// <summary>
    /// Генерирует MTL файл со всеми материалами сцены
    /// </summary>
    public static string GenerateSceneMaterials(ProceduralLevelGenerator.GeneratedLevel level)
    {
        var materials = new Dictionary<string, Material>();
        var used = new HashSet<string>();

        // Платформы
        foreach (var platform in level.Platforms)
        {
            string key = platform.Type ?? "normal";
            used.Add(key);
            if (!materials.ContainsKey(key))
            {
                materials[key] = CreatePlatformMaterial(key);
            }
        }

        // Враги
        foreach (var enemy in level.Enemies)
        {
            string key = enemy.Type ?? "walker";
            used.Add(key);
            if (!materials.ContainsKey(key))
            {
                materials[key] = CreateEnemyMaterial(key);
            }
        }

        // Собираемые предметы
        foreach (var item in level.Collectibles)
        {
            string key = item.Type ?? "coin";
            used.Add(key);
            if (!materials.ContainsKey(key))
            {
                materials[key] = CreateCollectibleMaterial(key);
            }
        }

        // Препятствия
        foreach (var obstacle in level.Obstacles)
        {
            string key = obstacle.Type ?? "box";
            used.Add(key);
            if (!materials.ContainsKey(key))
            {
                materials[key] = CreateObstacleMaterial(key);
            }
        }

        // Фон
        materials["background"] = new Material
        {
            Name = "background",
            Diffuse = new Color(50, 50, 100),
            Specular = new Color(100, 100, 150),
            Shininess = 32
        };

        var sb = new StringBuilder();
        sb.AppendLine("# Generated Materials");
        sb.AppendLine($"# Scene: Level {level.LevelId}");
        sb.AppendLine($"# Generated at: {DateTime.UtcNow:O}");
        sb.AppendLine();

        foreach (var mat in materials.Values)
        {
            sb.Append(mat.ToString());
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Генерирует простую процедурную текстуру в виде PPM файла
    /// </summary>
    public static string GenerateProceduralTexture(string name, float scale = 50)
    {
        int width = 256;
        int height = 256;

        var sb = new StringBuilder();
        sb.AppendLine("P3");
        sb.AppendLine($"# Generated procedural texture: {name}");
        sb.AppendLine($"{width} {height}");
        sb.AppendLine("255");

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Perlin noise для естественной текстуры
                float nx = x / scale;
                float ny = y / scale;
                float noise = PerlinNoise.Noise(nx, ny);
                float value = (noise + 1) / 2; // Нормализовать от 0 к 1

                // Цветовая вариация
                byte r = (byte)(value * 255);
                byte g = (byte)(value * 200);
                byte b = (byte)(value * 150);

                sb.Append($"{r} {g} {b} ");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Генерирует отчёт с информацией о материалах и текстурах
    /// </summary>
    public static string GenerateReport(ProceduralLevelGenerator.GeneratedLevel level)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Texture & Material Report ===");
        sb.AppendLine($"Level: {level.LevelId}");
        sb.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
        sb.AppendLine();

        // Статистика платформ по типам
        sb.AppendLine("--- Platform Materials ---");
        var platformTypes = level.Platforms.GroupBy(p => p.Type ?? "normal");
        foreach (var group in platformTypes)
        {
            sb.AppendLine($"{group.Key}: {group.Count()} platforms");
        }
        sb.AppendLine();

        // Статистика врагов по типам
        sb.AppendLine("--- Enemy Materials ---");
        var enemyTypes = level.Enemies.GroupBy(e => e.Type ?? "walker");
        foreach (var group in enemyTypes)
        {
            sb.AppendLine($"{group.Key}: {group.Count()} enemies");
        }
        sb.AppendLine();

        // Статистика собираемых предметов
        sb.AppendLine("--- Collectible Materials ---");
        var itemTypes = level.Collectibles.GroupBy(c => c.Type ?? "coin");
        foreach (var group in itemTypes)
        {
            sb.AppendLine($"{group.Key}: {group.Count()} items");
        }
        sb.AppendLine();

        // Статистика препятствий
        sb.AppendLine("--- Obstacle Materials ---");
        var obstacleTypes = level.Obstacles.GroupBy(o => o.Type ?? "box");
        foreach (var group in obstacleTypes)
        {
            sb.AppendLine($"{group.Key}: {group.Count()} obstacles");
        }
        sb.AppendLine();

        sb.AppendLine("=== Recommended Texture Settings ===");
        sb.AppendLine("Texture Resolution: 256x256");
        sb.AppendLine("Format: PPM (for procedural) or PNG (for direct)");
        sb.AppendLine("Ambient Lighting: 0.2");
        sb.AppendLine("Specular Mapping: Enabled");
        sb.AppendLine("Normal Mapping: Recommended for detail");

        return sb.ToString();
    }
}
