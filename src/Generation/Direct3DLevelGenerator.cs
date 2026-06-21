using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

/// <summary>
/// Прямая генерация 3D уровней из фото (ПРОПУСКАЕМ 2D)
/// Анализ фото → Создание 3D геометрии точно как в фото
/// </summary>
public class Direct3DLevelGenerator
{
    private readonly AIImageAnalyzer _analyzer;
    private readonly Mesh3DGenerator _meshGenerator;
    private readonly TextureGenerator _textureGenerator;

    public Direct3DLevelGenerator()
    {
        _analyzer = new AIImageAnalyzer();
        _meshGenerator = new Mesh3DGenerator();
        _textureGenerator = new TextureGenerator();
    }

    public class Direct3DResult
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("level_id")]
        public int LevelId { get; set; }

        [JsonPropertyName("analysis")]
        public AIImageAnalyzer.SceneAnalysis Analysis { get; set; }

        [JsonPropertyName("generation_method")]
        public string GenerationMethod { get; set; } = "Direct3D-from-Photo";

        [JsonPropertyName("objects_detected")]
        public int ObjectsDetected { get; set; }

        [JsonPropertyName("geometry_stats")]
        public GeometryStats Stats { get; set; }

        [JsonPropertyName("generation_time_ms")]
        public long GenerationTimeMs { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; } = "3.0-Direct3D";

        [JsonIgnore]
        public List<Mesh3DGenerator.Mesh> Meshes { get; set; } = new();
    }

    public class GeometryStats
    {
        [JsonPropertyName("total_meshes")]
        public int TotalMeshes { get; set; }

        [JsonPropertyName("total_vertices")]
        public int TotalVertices { get; set; }

        [JsonPropertyName("total_triangles")]
        public int TotalTriangles { get; set; }

        [JsonPropertyName("bounding_box")]
        public Dictionary<string, float> BoundingBox { get; set; }

        [JsonPropertyName("scene_volume")]
        public float SceneVolume { get; set; }

        [JsonPropertyName("detected_platforms")]
        public int DetectedPlatforms { get; set; }

        [JsonPropertyName("detected_obstacles")]
        public int DetectedObstacles { get; set; }

        [JsonPropertyName("detected_collectibles")]
        public int DetectedCollectibles { get; set; }
    }

    public Direct3DResult GenerateDirect3D(string imagePath, int levelId = 1)
    {
        var startTime = DateTime.Now;
        var result = new Direct3DResult 
        { 
            LevelId = levelId,
            Stats = new GeometryStats(),
            Meshes = new List<Mesh3DGenerator.Mesh>()
        };

        try
        {
            Console.WriteLine("[3D] Анализирую фото через ИИ...");
            var analysis = _analyzer.Analyze(imagePath);
            return GenerateDirect3DFromAnalysis(analysis, levelId, startTime);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Ошибка 3D генерации: {ex.Message}");
            result.Success = false;
        }

        result.GenerationTimeMs = (long)(DateTime.Now - startTime).TotalMilliseconds;
        return result;
    }

    public Direct3DResult GenerateDirect3DFromAnalysis(AIImageAnalyzer.SceneAnalysis analysis, int levelId = 1)
    {
        return GenerateDirect3DFromAnalysis(analysis, levelId, DateTime.Now);
    }

    private Direct3DResult GenerateDirect3DFromAnalysis(AIImageAnalyzer.SceneAnalysis analysis, int levelId, DateTime startTime)
    {
        var result = new Direct3DResult
        {
            LevelId = levelId,
            Stats = new GeometryStats(),
            Meshes = new List<Mesh3DGenerator.Mesh>(),
            Analysis = analysis
        };

        try
        {
            result.ObjectsDetected = analysis.Objects.Count;

            if (analysis.Objects.Count == 0)
            {
                result.Success = false;
                result.GenerationTimeMs = (long)(DateTime.Now - startTime).TotalMilliseconds;
                return result;
            }

            Console.WriteLine($"[3D] Конвертирую {analysis.Objects.Count} объектов в 3D...");
            var meshes = ConvertObjectsTo3D(analysis);
            result.Meshes = meshes;

            result.Stats.TotalMeshes = meshes.Count;
            result.Stats.TotalVertices = meshes.Sum(m => m.Vertices.Count);
            result.Stats.TotalTriangles = meshes.Sum(m => m.Indices.Count / 3);
            result.Stats.DetectedPlatforms = analysis.Objects.Count(o => o.Type == "platform");
            result.Stats.DetectedCollectibles = analysis.Objects.Count(o => o.Type == "collectible");
            result.Stats.DetectedObstacles = analysis.Objects.Count(o => o.Type == "obstacle");

            if (meshes.Count > 0)
            {
                ComputeBoundingBox(meshes, result.Stats);
            }

            result.Success = true;
            Console.WriteLine($"✓ 3D уровень создан: {result.Stats.TotalMeshes} сеток, {result.Stats.TotalVertices} вершин");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Ошибка 3D генерации: {ex.Message}");
            result.Success = false;
        }

        result.GenerationTimeMs = (long)(DateTime.Now - startTime).TotalMilliseconds;
        return result;
    }

    private List<Mesh3DGenerator.Mesh> ConvertObjectsTo3D(AIImageAnalyzer.SceneAnalysis analysis)
    {
        var meshes = new List<Mesh3DGenerator.Mesh>();

        // Конвертируем обнаруженные объекты в 3D
        foreach (var obj in analysis.Objects)
        {
            Mesh3DGenerator.Mesh mesh = null;

            if (obj.Type == "platform")
            {
                mesh = Mesh3DGenerator.CreateCubeMesh(
                    width: Math.Max(1f, obj.Width * 0.1f),
                    height: 0.3f,
                    depth: Math.Max(1f, obj.Height * 0.1f),
                    name: $"platform_{obj.Id}"
                );
            }
            else if (obj.Type == "obstacle")
            {
                mesh = Mesh3DGenerator.CreateCubeMesh(
                    width: Math.Max(1f, obj.Width * 0.1f),
                    height: Math.Max(1f, obj.Height * 0.1f),
                    depth: 0.5f,
                    name: $"obstacle_{obj.Id}"
                );
            }

            if (mesh != null)
            {
                mesh.Material = obj.Type;
                meshes.Add(mesh);
            }
        }

        // Если нет объектов, создаём базовое основание
        if (meshes.Count == 0)
        {
            Console.WriteLine("[3D] Создаю базовое основание...");
            var baseMesh = Mesh3DGenerator.CreateCubeMesh(
                width: 10f,
                height: 0.5f,
                depth: 10f,
                name: "base"
            );
            if (baseMesh != null)
            {
                baseMesh.Material = "base";
                meshes.Add(baseMesh);
            }
        }

        return meshes;
    }

    private List<Mesh3DGenerator.Mesh> CreateBaseLevelGeometry(AIImageAnalyzer.SceneAnalysis analysis)
    {
        var meshes = new List<Mesh3DGenerator.Mesh>();
        var complexity = analysis.Complexity;

        int platformLevels = (int)(2 + complexity * 5);

        Console.WriteLine($"[3D] Генерирую {platformLevels} уровней платформ (сложность {complexity:P})");

        for (int level = 0; level < platformLevels; level++)
        {
            float width = 4f + (float)new Random(level).NextDouble() * 4f;

            var platformMesh = Mesh3DGenerator.CreateCubeMesh(
                width: width,
                height: 0.5f,
                depth: 3f,
                name: $"platform_{level}"
            );

            if (platformMesh != null)
            {
                platformMesh.Material = "platform";
                meshes.Add(platformMesh);
            }

            if (level < platformLevels - 1 && complexity > 0.4f)
            {
                var obstacleMesh = Mesh3DGenerator.CreateCubeMesh(
                    width: 1f,
                    height: 2f,
                    depth: 1f,
                    name: $"obstacle_{level}"
                );

                if (obstacleMesh != null)
                {
                    obstacleMesh.Material = "obstacle";
                    meshes.Add(obstacleMesh);
                }
            }
        }

        return meshes;
    }

    private void ComputeBoundingBox(List<Mesh3DGenerator.Mesh> meshes, GeometryStats stats)
    {
        if (meshes.Count == 0) return;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var mesh in meshes)
        {
            foreach (var vertex in mesh.Vertices)
            {
                minX = Math.Min(minX, vertex.Position.X);
                maxX = Math.Max(maxX, vertex.Position.X);
                minY = Math.Min(minY, vertex.Position.Y);
                maxY = Math.Max(maxY, vertex.Position.Y);
                minZ = Math.Min(minZ, vertex.Position.Z);
                maxZ = Math.Max(maxZ, vertex.Position.Z);
            }
        }

        stats.BoundingBox = new Dictionary<string, float>
        {
            { "min_x", minX },
            { "max_x", maxX },
            { "min_y", minY },
            { "max_y", maxY },
            { "min_z", minZ },
            { "max_z", maxZ }
        };

        float volumeX = maxX - minX;
        float volumeY = maxY - minY;
        float volumeZ = maxZ - minZ;
        stats.SceneVolume = volumeX * volumeY * volumeZ;
    }

    public static string ExportMeshesToOBJ(List<Mesh3DGenerator.Mesh> meshes)
    {
        var objContent = new System.Text.StringBuilder();
        objContent.AppendLine("# Direct3D Level Export - OBJ Format");
        objContent.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        objContent.AppendLine($"# Total Meshes: {meshes.Count}");
        objContent.AppendLine();

        int globalVertexOffset = 0;

        foreach (var mesh in meshes)
        {
            objContent.AppendLine($"g {mesh.Name ?? "mesh"}");
            
            foreach (var vertex in mesh.Vertices)
            {
                objContent.AppendLine(FormattableString.Invariant($"v {vertex.Position.X:F4} {vertex.Position.Y:F4} {vertex.Position.Z:F4}"));
            }

            foreach (var vertex in mesh.Vertices)
            {
                objContent.AppendLine(FormattableString.Invariant($"vn {vertex.Normal.X:F4} {vertex.Normal.Y:F4} {vertex.Normal.Z:F4}"));
            }

            foreach (var vertex in mesh.Vertices)
            {
                objContent.AppendLine(FormattableString.Invariant($"vt {vertex.TexCoord.X:F4} {vertex.TexCoord.Y:F4}"));
            }

            if (mesh.Material != null)
                objContent.AppendLine($"usemtl {mesh.Material}");

            for (int i = 0; i < mesh.Indices.Count; i += 3)
            {
                int i1 = mesh.Indices[i] + globalVertexOffset + 1;
                int i2 = mesh.Indices[i + 1] + globalVertexOffset + 1;
                int i3 = mesh.Indices[i + 2] + globalVertexOffset + 1;

                objContent.AppendLine($"f {i1}/{i1}/{i1} {i2}/{i2}/{i2} {i3}/{i3}/{i3}");
            }

            globalVertexOffset += mesh.Vertices.Count;
            objContent.AppendLine();
        }

        return objContent.ToString();
    }

    public static string ExportMeshesToGLTF(List<Mesh3DGenerator.Mesh> meshes)
    {
        var gltfJson = new System.Text.StringBuilder();
        gltfJson.AppendLine("{");
        gltfJson.AppendLine("  \"asset\": { \"version\": \"2.0\" },");
        gltfJson.AppendLine("  \"scene\": 0,");
        gltfJson.AppendLine("  \"scenes\": [{ \"nodes\": [0] }],");
        gltfJson.AppendLine("  \"nodes\": [{ \"mesh\": 0 }],");
        gltfJson.AppendLine("  \"meshes\": [{ \"primitives\": [{}] }],");
        gltfJson.AppendLine("  \"accessors\": [],");
        gltfJson.AppendLine("  \"bufferViews\": [],");
        gltfJson.AppendLine("  \"buffers\": []");
        gltfJson.AppendLine("}");

        return gltfJson.ToString();
    }
}
