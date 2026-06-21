using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

/// <summary>
/// Экспортер 3D уровней в различные форматы (OBJ, GLTF, FBX)
/// </summary>
public class Level3DExporter
{
    /// <summary>
    /// Экспортирует уровень в OBJ формат
    /// </summary>
    public static string ExportToOBJ(ProceduralLevelGenerator.GeneratedLevel level, 
                                      TextureGenerator.Material[] materials = null)
    {
        var meshes = new List<Mesh3DGenerator.Mesh>();
        var sb = new StringBuilder();

        sb.AppendLine("# Блатформер уровень - OBJ формат");
        sb.AppendLine($"# Уровень {level.LevelId}");
        sb.AppendLine($"# Размер: {level.Width}x{level.Height}");
        sb.AppendLine($"# Сгенерировано: {DateTime.UtcNow:O}");
        sb.AppendLine();
        sb.AppendLine("mtllib level.mtl");
        sb.AppendLine();

        int vertexOffset = 0;

        // Экспортируем платформы
        sb.AppendLine("# === ПЛАТФОРМЫ ===");
        foreach (var platform in level.Platforms)
        {
            var mesh = Mesh3DGenerator.PlatformTo3D(platform);
            sb.AppendLine();
            sb.Append(mesh.ToOBJ(vertexOffset));
            vertexOffset += mesh.GetVertexCount();
        }

        // Экспортируем врагов
        sb.AppendLine();
        sb.AppendLine("# === ВРАГИ ===");
        foreach (var enemy in level.Enemies)
        {
            var mesh = Mesh3DGenerator.EnemyTo3D(enemy);
            sb.AppendLine();
            sb.Append(mesh.ToOBJ(vertexOffset));
            vertexOffset += mesh.GetVertexCount();
        }

        // Экспортируем собираемые предметы
        sb.AppendLine();
        sb.AppendLine("# === СОБИРАЕМЫЕ ПРЕДМЕТЫ ===");
        foreach (var item in level.Collectibles)
        {
            var mesh = Mesh3DGenerator.CollectibleTo3D(item);
            sb.AppendLine();
            sb.Append(mesh.ToOBJ(vertexOffset));
            vertexOffset += mesh.GetVertexCount();
        }

        // Экспортируем препятствия
        sb.AppendLine();
        sb.AppendLine("# === ПРЕПЯТСТВИЯ ===");
        foreach (var obstacle in level.Obstacles)
        {
            var mesh = Mesh3DGenerator.ObstacleTo3D(obstacle);
            sb.AppendLine();
            sb.Append(mesh.ToOBJ(vertexOffset));
            vertexOffset += mesh.GetVertexCount();
        }

        // Фон/земля
        sb.AppendLine();
        sb.AppendLine("# === ФОНОВЫЕ ЭЛЕМЕНТЫ ===");
        var floorMesh = Mesh3DGenerator.CreateCubeMesh(level.Width + 100, 10, 100, "floor");
        floorMesh.Position = new Mesh3DGenerator.Vector3(level.Width / 2, 600, 0);
        sb.Append(floorMesh.ToOBJ(vertexOffset));

        return sb.ToString();
    }

    /// <summary>
    /// Экспортирует уровень в GLTF JSON формат
    /// </summary>
    public static string ExportToGLTF(ProceduralLevelGenerator.GeneratedLevel level)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("{");
        sb.AppendLine("  \"asset\": {");
        sb.AppendLine("    \"generator\": \"Hybrid Level Generator 2.0\",");
        sb.AppendLine("    \"version\": \"2.0\"");
        sb.AppendLine("  },");
        sb.AppendLine("  \"scene\": 0,");
        sb.AppendLine("  \"scenes\": [{");
        sb.AppendLine("    \"nodes\": [0]");
        sb.AppendLine("  }],");
        sb.AppendLine("  \"nodes\": [{");
        sb.AppendLine("    \"mesh\": 0,");
        sb.AppendLine("    \"name\": \"Level\"");
        sb.AppendLine("  }],");
        sb.AppendLine("  \"meshes\": [{");
        sb.AppendLine("    \"name\": \"LevelMesh\",");
        sb.AppendLine("    \"primitives\": [");

        var primitives = new List<string>();

        // Добавляем примитивы для каждого типа объекта
        int meshIndex = 0;

        // Платформы
        foreach (var platform in level.Platforms)
        {
            primitives.Add(CreateGLTFPrimitive(meshIndex++, "platform", platform.Type ?? "normal"));
        }

        // Враги
        foreach (var enemy in level.Enemies)
        {
            primitives.Add(CreateGLTFPrimitive(meshIndex++, "enemy", enemy.Type ?? "walker"));
        }

        // Собираемые предметы
        foreach (var item in level.Collectibles)
        {
            primitives.Add(CreateGLTFPrimitive(meshIndex++, "collectible", item.Type ?? "coin"));
        }

        // Препятствия
        foreach (var obstacle in level.Obstacles)
        {
            primitives.Add(CreateGLTFPrimitive(meshIndex++, "obstacle", obstacle.Type ?? "box"));
        }

        sb.AppendLine(string.Join(",\n", primitives));
        sb.AppendLine("    ]");
        sb.AppendLine("  }],");
        sb.AppendLine("  \"materials\": [");

        var materials = new List<string>
        {
            CreateGLTFMaterial("platform", new TextureGenerator.Color(120, 120, 120)),
            CreateGLTFMaterial("moving_platform", new TextureGenerator.Color(100, 150, 200)),
            CreateGLTFMaterial("bouncy_platform", new TextureGenerator.Color(255, 150, 0)),
            CreateGLTFMaterial("fragile_platform", new TextureGenerator.Color(200, 100, 100)),
            CreateGLTFMaterial("walker_enemy", new TextureGenerator.Color(150, 50, 150)),
            CreateGLTFMaterial("flyer_enemy", new TextureGenerator.Color(200, 50, 50)),
            CreateGLTFMaterial("turret_enemy", new TextureGenerator.Color(100, 100, 100)),
            CreateGLTFMaterial("coin_material", new TextureGenerator.Color(255, 200, 0)),
            CreateGLTFMaterial("crystal_material", new TextureGenerator.Color(0, 255, 200)),
            CreateGLTFMaterial("powerup_material", new TextureGenerator.Color(255, 255, 0)),
            CreateGLTFMaterial("spike_material", new TextureGenerator.Color(100, 50, 0)),
            CreateGLTFMaterial("fire_material", new TextureGenerator.Color(255, 100, 0)),
            CreateGLTFMaterial("acid_material", new TextureGenerator.Color(50, 200, 50))
        };

        sb.AppendLine(string.Join(",\n", materials));
        sb.AppendLine("  ],");
        sb.AppendLine("  \"accessors\": [],");
        sb.AppendLine("  \"bufferViews\": [],");
        sb.AppendLine("  \"buffers\": []");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Генерирует примитив для GLTF
    /// </summary>
    private static string CreateGLTFPrimitive(int index, string type, string subtype)
    {
        return $@"      {{
        ""attributes"": {{}},
        ""material"": {index},
        ""mode"": 4,
        ""name"": ""{type}_{subtype}_{index}""
      }}";
    }

    /// <summary>
    /// Генерирует материал для GLTF
    /// </summary>
    private static string CreateGLTFMaterial(string name, TextureGenerator.Color color)
    {
        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;

        return $@"    {{
      ""name"": ""{name}"",
      ""pbrMetallicRoughness"": {{
        ""baseColorFactor"": [{r:F3}, {g:F3}, {b:F3}, 1.0],
        ""metallicFactor"": 0.5,
        ""roughnessFactor"": 0.5
      }}
    }}";
    }

    /// <summary>
    /// Создаёт файл конфигурации для Unity
    /// </summary>
    public static string GenerateUnityConfig(ProceduralLevelGenerator.GeneratedLevel level)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using UnityEngine.ProBuilder;");
        sb.AppendLine();
        sb.AppendLine("public class GeneratedLevel : MonoBehaviour");
        sb.AppendLine("{");
        sb.AppendLine("    public void CreateLevel()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Платформы");
        sb.AppendLine("        if (this.GetComponent<ProBuilderMesh>() == null)");
        sb.AppendLine("            ProBuilderMesh.Create();");
        sb.AppendLine();

        foreach (var platform in level.Platforms)
        {
            sb.AppendLine($"        // Platform {platform.Id}");
            sb.AppendLine($"        var platform{platform.Id} = GameObject.CreatePrimitive(PrimitiveType.Cube);");
            sb.AppendLine($"        platform{platform.Id}.transform.position = new Vector3({platform.X}, {platform.Y}, 0);");
            sb.AppendLine($"        platform{platform.Id}.transform.localScale = new Vector3({platform.Width}, 20, 50);");
            sb.AppendLine($"        platform{platform.Id}.name = \"Platform_{platform.Id}\";");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Создаёт файл конфигурации для Godot
    /// </summary>
    public static string GenerateGodotConfig(ProceduralLevelGenerator.GeneratedLevel level)
    {
        var sb = new StringBuilder();
        sb.AppendLine("extends Node3D");
        sb.AppendLine();
        sb.AppendLine("func _ready():");
        sb.AppendLine("    create_level()");
        sb.AppendLine();
        sb.AppendLine("func create_level():");

        foreach (var platform in level.Platforms)
        {
            sb.AppendLine($"    # Platform {platform.Id}");
            sb.AppendLine($"    var platform = BoxMesh.new()");
            sb.AppendLine($"    var mesh_instance = MeshInstance3D.new()");
            sb.AppendLine($"    mesh_instance.mesh = platform");
            sb.AppendLine($"    mesh_instance.position = Vector3({platform.X}, {platform.Y}, 0)");
            sb.AppendLine($"    mesh_instance.scale = Vector3({platform.Width}/100.0, 20/100.0, 50/100.0)");
            sb.AppendLine($"    add_child(mesh_instance)");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Создаёт JSON манифест со всеми экспортированными файлами
    /// </summary>
    public static string GenerateExportManifest(ProceduralLevelGenerator.GeneratedLevel level,
                                                string objFileName = "level.obj",
                                                string mtlFileName = "level.mtl",
                                                string gltfFileName = "level.gltf")
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"level_id\": {level.LevelId},");
        sb.AppendLine($"  \"export_timestamp\": \"{DateTime.UtcNow:O}\",");
        sb.AppendLine($"  \"level_dimensions\": {{");
        sb.AppendLine($"    \"width\": {level.Width},");
        sb.AppendLine($"    \"height\": {level.Height}");
        sb.AppendLine("  },");
        sb.AppendLine($"  \"statistics\": {{");
        sb.AppendLine($"    \"platforms\": {level.Platforms.Count},");
        sb.AppendLine($"    \"enemies\": {level.Enemies.Count},");
        sb.AppendLine($"    \"collectibles\": {level.Collectibles.Count},");
        sb.AppendLine($"    \"obstacles\": {level.Obstacles.Count},");
        sb.AppendLine($"    \"total_objects\": {level.Platforms.Count + level.Enemies.Count + level.Collectibles.Count + level.Obstacles.Count}");
        sb.AppendLine("  },");
        sb.AppendLine($"  \"files\": {{");
        sb.AppendLine($"    \"obj\": \"{objFileName}\",");
        sb.AppendLine($"    \"mtl\": \"{mtlFileName}\",");
        sb.AppendLine($"    \"gltf\": \"{gltfFileName}\",");
        sb.AppendLine($"    \"unity_config\": \"GeneratedLevel.cs\",");
        sb.AppendLine($"    \"godot_config\": \"GeneratedLevel.gd\"");
        sb.AppendLine("  },");
        sb.AppendLine($"  \"formats_supported\": [\"OBJ\", \"GLTF\", \"Unity\", \"Godot\"],");
        sb.AppendLine($"  \"player_start\": {{\"{level.PlayerStart.x}\": {level.PlayerStart.y}}},");
        sb.AppendLine($"  \"player_end\": {{\"{level.PlayerEnd.x}\": {level.PlayerEnd.y}}},");
        sb.AppendLine($"  \"estimated_difficulty\": {level.EstimatedDifficulty:F3}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Экспортирует уровень в полный пакет (все форматы сразу)
    /// </summary>
    public static Dictionary<string, string> ExportLevelPackage(ProceduralLevelGenerator.GeneratedLevel level)
    {
        var files = new Dictionary<string, string>();

        // OBJ формат
        files["level.obj"] = ExportToOBJ(level);
        files["level.mtl"] = TextureGenerator.GenerateSceneMaterials(level);

        // GLTF формат
        files["level.gltf"] = ExportToGLTF(level);

        // Конфигурации для игровых движков
        files["GeneratedLevel.cs"] = GenerateUnityConfig(level);
        files["GeneratedLevel.gd"] = GenerateGodotConfig(level);

        // Текстуры (процедурные как PPM)
        files["texture_procedural.ppm"] = TextureGenerator.GenerateProceduralTexture("level_base", 50);

        // Отчёты
        files["materials_report.txt"] = TextureGenerator.GenerateReport(level);
        files["export_manifest.json"] = GenerateExportManifest(level);

        return files;
    }

    /// <summary>
    /// Сохраняет пакет на диск в указанную папку
    /// </summary>
    public static void SaveLevelPackage(ProceduralLevelGenerator.GeneratedLevel level, string outputPath)
    {
        var files = ExportLevelPackage(level);
        
        Directory.CreateDirectory(outputPath);

        foreach (var kvp in files)
        {
            string filePath = Path.Combine(outputPath, kvp.Key);
            File.WriteAllText(filePath, kvp.Value);
            Console.WriteLine($"✅ Сохранён: {kvp.Key}");
        }

        Console.WriteLine($"\n📦 Пакет сохранён в: {outputPath}");
    }
}
