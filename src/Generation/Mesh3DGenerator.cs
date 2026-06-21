using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// 3D генератор мешей на основе 2D уровня
/// Создаёт объёмные платформы, врагов, препятствия с текстурами
/// </summary>
public class Mesh3DGenerator
{
    /// <summary>
    /// 3D вектор
    /// </summary>
    public struct Vector3
    {
        public float X, Y, Z;

        public Vector3(float x = 0, float y = 0, float z = 0)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString() => $"{X:F2},{Y:F2},{Z:F2}";

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.X * s, a.Y * s, a.Z * s);
    }

    /// <summary>
    /// 2D координата на текстуре
    /// </summary>
    public struct Vector2
    {
        public float X, Y;

        public Vector2(float x = 0, float y = 0)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"{X:F2},{Y:F2}";
    }

    /// <summary>
    /// Вершина с позицией, нормалью и текстурными координатами
    /// </summary>
    public class Vertex
    {
        public Vector3 Position { get; set; }
        public Vector3 Normal { get; set; }
        public Vector2 TexCoord { get; set; }

        public Vertex(Vector3 pos, Vector3 normal = default, Vector2 tex = default)
        {
            Position = pos;
            Normal = normal;
            TexCoord = tex;
        }

        public override string ToString() => $"v {Position}";
    }

    /// <summary>
    /// 3D сетка с вершинами, индексами, нормалями
    /// </summary>
    public class Mesh
    {
        public string Name { get; set; }
        public List<Vertex> Vertices { get; set; } = new();
        public List<int> Indices { get; set; } = new();
        public string Material { get; set; } = "default";
        public Vector3 Position { get; set; } = new(0, 0, 0);
        public Vector3 Scale { get; set; } = new(1, 1, 1);
        public Vector3 Rotation { get; set; } = new(0, 0, 0); // Euler углы в радианах

        /// <summary>
        /// Экспортирует в строку формата OBJ
        /// </summary>
        public string ToOBJ(int vertexOffset = 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"g {Name}");
            sb.AppendLine($"usemtl {Material}");

            foreach (var v in Vertices)
            {
                sb.AppendLine($"v {v.Position.X:F3} {v.Position.Y:F3} {v.Position.Z:F3}");
            }

            foreach (var v in Vertices)
            {
                sb.AppendLine($"vn {v.Normal.X:F3} {v.Normal.Y:F3} {v.Normal.Z:F3}");
            }

            foreach (var v in Vertices)
            {
                sb.AppendLine($"vt {v.TexCoord.X:F3} {v.TexCoord.Y:F3}");
            }

            for (int i = 0; i < Indices.Count; i += 3)
            {
                int i1 = Indices[i] + vertexOffset + 1;
                int i2 = Indices[i + 1] + vertexOffset + 1;
                int i3 = Indices[i + 2] + vertexOffset + 1;

                sb.AppendLine($"f {i1}/{i1}/{i1} {i2}/{i2}/{i2} {i3}/{i3}/{i3}");
            }

            return sb.ToString();
        }

        public int GetVertexCount() => Vertices.Count;
        public int GetTriangleCount() => Indices.Count / 3;
    }

    /// <summary>
    /// Генерирует куб (платформа)
    /// </summary>
    public static Mesh CreateCubeMesh(float width, float height, float depth, string name = "platform")
    {
        var mesh = new Mesh { Name = name };

        // Половины размеров
        float w = width / 2;
        float h = height / 2;
        float d = depth / 2;

        // 8 вершин куба
        var vertices = new[]
        {
            new Vertex(new Vector3(-w, -h, -d), new Vector3(-1, 0, 0), new Vector2(0, 0)),      // 0 - левый передний нижний
            new Vertex(new Vector3(-w, h, -d), new Vector3(-1, 0, 0), new Vector2(0, 1)),       // 1 - левый передний верхний
            new Vertex(new Vector3(w, h, -d), new Vector3(0, 0, -1), new Vector2(1, 1)),        // 2 - правый передний верхний
            new Vertex(new Vector3(w, -h, -d), new Vector3(0, 0, -1), new Vector2(1, 0)),       // 3 - правый передний нижний
            new Vertex(new Vector3(w, -h, d), new Vector3(1, 0, 0), new Vector2(0, 0)),         // 4 - правый задний нижний
            new Vertex(new Vector3(w, h, d), new Vector3(1, 0, 0), new Vector2(0, 1)),          // 5 - правый задний верхний
            new Vertex(new Vector3(-w, h, d), new Vector3(0, 0, 1), new Vector2(1, 1)),         // 6 - левый задний верхний
            new Vertex(new Vector3(-w, -h, d), new Vector3(0, 0, 1), new Vector2(1, 0))         // 7 - левый задний нижний
        };

        mesh.Vertices.AddRange(vertices);

        // Индексы (6 граней × 2 треугольника × 3 индекса = 36)
        int[] indices =
        {
            // Передняя грань (z = -1)
            0, 2, 1,
            0, 3, 2,
            // Задняя грань (z = 1)
            4, 6, 5,
            4, 7, 6,
            // Левая грань (x = -1)
            7, 1, 6,
            7, 0, 1,
            // Правая грань (x = 1)
            3, 5, 4,
            3, 2, 5,
            // Верхняя грань (y = 1)
            1, 5, 6,
            1, 2, 5,
            // Нижняя грань (y = -1)
            7, 3, 4,
            7, 0, 3
        };

        mesh.Indices.AddRange(indices);

        return mesh;
    }

    /// <summary>
    /// Генерирует цилиндр (для врагов, колонн)
    /// </summary>
    public static Mesh CreateCylinderMesh(float radius, float height, int segments = 16, string name = "cylinder")
    {
        var mesh = new Mesh { Name = name };
        float h = height / 2;

        // Верхние и нижние вершины
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)(2 * Math.PI * i / segments);
            float x = radius * (float)Math.Cos(angle);
            float z = radius * (float)Math.Sin(angle);

            // Нижняя окружность
            mesh.Vertices.Add(new Vertex(
                new Vector3(x, -h, z),
                new Vector3((float)Math.Cos(angle), 0, (float)Math.Sin(angle)),
                new Vector2((float)i / segments, 0)
            ));

            // Верхняя окружность
            mesh.Vertices.Add(new Vertex(
                new Vector3(x, h, z),
                new Vector3((float)Math.Cos(angle), 0, (float)Math.Sin(angle)),
                new Vector2((float)i / segments, 1)
            ));
        }

        // Центры кругов (для крышек)
        int bottomCenter = mesh.Vertices.Count;
        mesh.Vertices.Add(new Vertex(new Vector3(0, -h, 0), new Vector3(0, -1, 0), new Vector2(0.5f, 0.5f)));

        int topCenter = mesh.Vertices.Count;
        mesh.Vertices.Add(new Vertex(new Vector3(0, h, 0), new Vector3(0, 1, 0), new Vector2(0.5f, 0.5f)));

        // Боковые треугольники
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            int b1 = i * 2;
            int b2 = next * 2;
            int t1 = i * 2 + 1;
            int t2 = next * 2 + 1;

            // Боковые грани
            mesh.Indices.AddRange(new[] { b1, t1, b2 });
            mesh.Indices.AddRange(new[] { b2, t1, t2 });
        }

        // Нижняя крышка
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            mesh.Indices.AddRange(new[] { bottomCenter, next * 2, i * 2 });
        }

        // Верхняя крышка
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            mesh.Indices.AddRange(new[] { topCenter, i * 2 + 1, next * 2 + 1 });
        }

        return mesh;
    }

    /// <summary>
    /// Генерирует сферу (для collectibles)
    /// </summary>
    public static Mesh CreateSphereMesh(float radius, int segments = 8, int rings = 4, string name = "sphere")
    {
        var mesh = new Mesh { Name = name };

        // Генерируем вершины для сферы
        for (int ring = 0; ring <= rings; ring++)
        {
            float v = (float)ring / rings;
            float phi = (float)Math.PI * v;
            float sinPhi = (float)Math.Sin(phi);
            float cosPhi = (float)Math.Cos(phi);

            for (int seg = 0; seg < segments; seg++)
            {
                float u = (float)seg / segments;
                float theta = (float)(2 * Math.PI * u);
                float sinTheta = (float)Math.Sin(theta);
                float cosTheta = (float)Math.Cos(theta);

                float x = radius * sinPhi * cosTheta;
                float y = radius * cosPhi;
                float z = radius * sinPhi * sinTheta;

                var normal = new Vector3(x / radius, y / radius, z / radius);
                mesh.Vertices.Add(new Vertex(
                    new Vector3(x, y, z),
                    normal,
                    new Vector2(u, v)
                ));
            }
        }

        // Генерируем индексы
        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                int current = ring * segments + seg;
                int next = current + 1;
                if (next % segments == 0) next -= segments;

                int below = (ring + 1) * segments + seg;
                int belowNext = below + 1;
                if (belowNext % segments == 0) belowNext -= segments;

                mesh.Indices.AddRange(new[] { current, below, next });
                mesh.Indices.AddRange(new[] { next, below, belowNext });
            }
        }

        return mesh;
    }

    /// <summary>
    /// Генерирует пирамиду (препятствие)
    /// </summary>
    public static Mesh CreatePyramidMesh(float baseSize, float height, string name = "pyramid")
    {
        var mesh = new Mesh { Name = name };

        float b = baseSize / 2;
        float h = height;

        // 5 вершин (4 угла основания + вершина)
        var vertices = new[]
        {
            new Vertex(new Vector3(-b, 0, -b), new Vector3(0, -1, 0), new Vector2(0, 0)),       // 0 - основание
            new Vertex(new Vector3(b, 0, -b), new Vector3(0, -1, 0), new Vector2(1, 0)),
            new Vertex(new Vector3(b, 0, b), new Vector3(0, -1, 0), new Vector2(1, 1)),
            new Vertex(new Vector3(-b, 0, b), new Vector3(0, -1, 0), new Vector2(0, 1)),
            new Vertex(new Vector3(0, h, 0), new Vector3(0, 1, 0), new Vector2(0.5f, 0.5f))    // 4 - вершина
        };

        mesh.Vertices.AddRange(vertices);

        // Индексы (основание + 4 боковые грани)
        int[] indices =
        {
            // Основание
            0, 2, 1,
            0, 3, 2,
            // Боковые грани
            0, 4, 1,
            1, 4, 2,
            2, 4, 3,
            3, 4, 0
        };

        mesh.Indices.AddRange(indices);

        return mesh;
    }

    /// <summary>
    /// Преобразует 2D платформу в 3D mesh
    /// </summary>
    public static Mesh PlatformTo3D(ProceduralLevelGenerator.PlatformElement platform, float thickness = 20)
    {
        string platformType = platform.Type switch
        {
            "moving" => "moving_platform",
            "bouncy" => "bouncy_platform",
            "fragile" => "fragile_platform",
            _ => "platform"
        };

        var mesh = CreateCubeMesh(platform.Width, thickness, 50, $"platform_{platform.Id}");
        mesh.Material = platformType;
        mesh.Position = new Vector3(platform.X, platform.Y, 0);

        return mesh;
    }

    /// <summary>
    /// Преобразует 2D врага в 3D mesh
    /// </summary>
    public static Mesh EnemyTo3D(ProceduralLevelGenerator.EnemyElement enemy, float scale = 30)
    {
        var mesh = CreateCylinderMesh(scale / 2, scale, 8, $"enemy_{enemy.Id}");

        mesh.Material = enemy.Type switch
        {
            "flyer" => "flyer_enemy",
            "turret" => "turret_enemy",
            _ => "walker_enemy"
        };

        mesh.Position = new Vector3(enemy.X, enemy.Y, 0);

        return mesh;
    }

    /// <summary>
    /// Преобразует собираемый предмет в 3D mesh
    /// </summary>
    public static Mesh CollectibleTo3D(ProceduralLevelGenerator.CollectibleElement item, float scale = 15)
    {
        var mesh = CreateSphereMesh(scale / 2, 6, 3, $"collectible_{item.Id}");

        mesh.Material = item.Type switch
        {
            "crystal" => "crystal_material",
            "powerup" => "powerup_material",
            _ => "coin_material"
        };

        mesh.Position = new Vector3(item.X, item.Y, 0);

        return mesh;
    }

    /// <summary>
    /// Преобразует препятствие в 3D mesh
    /// </summary>
    public static Mesh ObstacleTo3D(ProceduralLevelGenerator.ObstacleElement obstacle)
    {
        Mesh mesh;
        
        if (obstacle.Type == "spike")
        {
            mesh = CreatePyramidMesh(obstacle.Width, obstacle.Height, $"obstacle_{obstacle.Id}");
        }
        else
        {
            mesh = CreateCubeMesh(obstacle.Width, obstacle.Height, 30, $"obstacle_{obstacle.Id}");
        }

        mesh.Material = obstacle.Type switch
        {
            "spike" => "spike_material",
            "fire" => "fire_material",
            "acid" => "acid_material",
            _ => "obstacle_material"
        };

        mesh.Position = new Vector3(obstacle.X, obstacle.Y, 0);

        return mesh;
    }

    /// <summary>
    /// Раскрашивает сцену в разные цвета в зависимости от типа
    /// </summary>
    public static string GenerateSampleScene()
    {
        var meshes = new List<Mesh>
        {
            CreateCubeMesh(800, 20, 50, "floor"),
            CreateCylinderMesh(15, 30, 8, "enemy1"),
            CreateSphereMesh(10, 6, 3, "collectible1"),
            CreatePyramidMesh(40, 50, "spike1")
        };

        var sb = new StringBuilder();
        sb.AppendLine("# Sample 3D Scene");
        sb.AppendLine("# Generated by Mesh3DGenerator");
        sb.AppendLine();

        int vertexOffset = 0;
        foreach (var mesh in meshes)
        {
            sb.Append(mesh.ToOBJ(vertexOffset));
            sb.AppendLine();
            vertexOffset += mesh.GetVertexCount();
        }

        return sb.ToString();
    }
}
