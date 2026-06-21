using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// In-memory storage for current game level
string? currentGameLevel = null;

// World model engine (Yan-style: Sim/Gen/Edit) session storage
var worldEngine = new WorldModelEngine();
var worldSessions = new ConcurrentDictionary<string, WorldModelEngine.WorldState>();
var worldStorageDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "world_sessions");
Directory.CreateDirectory(worldStorageDir);

// ИИ-модель оценки глубины (Depth Anything V2) — реальное «понимание» сцены на фото.
var depthOptions = builder.Configuration.GetSection("DepthService").Get<DepthServiceOptions>() ?? new DepthServiceOptions();
DepthEstimationClient? depthClient = null;
if (depthOptions.Enabled)
{
    depthClient = new DepthEstimationClient(depthOptions.BaseUrl, TimeSpan.FromSeconds(depthOptions.TimeoutSeconds));
}

static (int size, List<float> values) BuildDepthGrid(AIImageAnalyzer.DepthMap? depthMap, int gridSize)
{
    if (depthMap == null || depthMap.Width <= 0 || depthMap.Height <= 0)
    {
        return (0, new List<float>());
    }

    var values = new float[gridSize * gridSize];

    int Index(int gx, int gy) => gy * gridSize + gx;

    for (int gy = 0; gy < gridSize; gy++)
    {
        for (int gx = 0; gx < gridSize; gx++)
        {
            int x0 = (int)Math.Floor(gx / (float)gridSize * depthMap.Width);
            int x1 = (int)Math.Floor((gx + 1) / (float)gridSize * depthMap.Width);
            int y0 = (int)Math.Floor(gy / (float)gridSize * depthMap.Height);
            int y1 = (int)Math.Floor((gy + 1) / (float)gridSize * depthMap.Height);

            x1 = Math.Clamp(x1, x0 + 1, depthMap.Width);
            y1 = Math.Clamp(y1, y0 + 1, depthMap.Height);

            float sum = 0f;
            int count = 0;
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    sum += depthMap.Values[y, x];
                    count++;
                }
            }

            values[Index(gx, gy)] = count > 0 ? Math.Clamp(sum / count, 0f, 1f) : 0f;
        }
    }

    return (gridSize, values.ToList());
}

static async Task<string> SaveUploadedImageAsync(HttpRequest request, ILogger logger, string baseName)
{
    var file = request.Form.Files[0];
    string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
    Directory.CreateDirectory(uploadPath);

    string fileExt = Path.GetExtension(file.FileName).ToLower();
    string inputPath;

    if (fileExt == ".jfif")
    {
        string tempJfifPath = Path.Combine(uploadPath, $"{baseName}.jfif");
        using (var stream = new FileStream(tempJfifPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        try
        {
            using (var jfifImage = SixLabors.ImageSharp.Image.Load(tempJfifPath))
            {
                inputPath = Path.Combine(uploadPath, $"{baseName}.png");
                using (var outputStream = new FileStream(inputPath, FileMode.Create))
                {
                    await jfifImage.SaveAsync(outputStream, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
                }
            }
            File.Delete(tempJfifPath);
            logger.LogInformation("Converted JFIF to PNG: {Path}", inputPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to convert JFIF to PNG");
            inputPath = tempJfifPath;
        }
    }
    else if (fileExt == ".jpg" || fileExt == ".jpeg")
    {
        inputPath = Path.Combine(uploadPath, $"{baseName}.jpg");
        using (var stream = new FileStream(inputPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }
    }
    else
    {
        inputPath = Path.Combine(uploadPath, $"{baseName}.png");
        using (var stream = new FileStream(inputPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }
    }

    logger.LogInformation("Image saved to: {Path}", inputPath);
    return inputPath;
}

static LevelDataContainer BuildLevelFromAnalysis(AIImageAnalyzer.SceneAnalysis analysis)
{
    var level = new LevelDataContainer
    {
        Width = analysis.ImageWidth,
        Height = analysis.ImageHeight
    };

    foreach (var obj in analysis.Objects)
    {
        if (obj.Type != "platform" && obj.Type != "obstacle")
            continue;

        int startX = obj.X;
        int endX = obj.X + obj.Width;
        int y = obj.Y + obj.Height;

        level.Platforms.Add(new PlatformData(
            new Vector2D(startX, y),
            new Vector2D(endX, y),
            20
        ));
    }

    return level;
}

app.Logger.LogInformation("=== Level Generator Web App Started ===");
app.Logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);

app.UseCors("AllowAll");
app.UseStaticFiles();

// API endpoint for 3D level generation (DIRECT 3D from Photo - skips 2D)
app.MapPost("/api/generate", async (HttpRequest request) =>
{
    app.Logger.LogInformation("Received generation request - DIRECT 3D MODE (Photo → AI → 3D)");
    
    try
    {
        if (!request.HasFormContentType || request.Form.Files.Count == 0)
        {
            return Results.BadRequest(new { error = "No image file provided" });
        }

        var file = request.Form.Files[0];
        app.Logger.LogInformation("Processing image: {FileName}", file.FileName);
        string inputPath = await SaveUploadedImageAsync(request, app.Logger, "input");

        // === PHOTO → AI DEPTH → ANALYSIS → 3D ===
        var direct3DGenerator = new Direct3DLevelGenerator();
        var analyzer = new AIImageAnalyzer();

        // Шаг 1: реальная карта глубины из нейросети Depth Anything V2 (если сервис доступен).
        AIImageAnalyzer.DepthMap? realDepth = null;
        var analysisSource = "classic-cv";
        if (depthClient != null)
        {
            try
            {
                realDepth = await depthClient.EstimateDepthAsync(inputPath);
                analysisSource = "depth-anything-v2";
                app.Logger.LogInformation("Depth Anything V2: real depth map {W}x{H}", realDepth.Width, realDepth.Height);
            }
            catch (Exception depthEx)
            {
                app.Logger.LogWarning(depthEx, "Depth service unavailable, falling back to brightness depth");
            }
        }

        // Шаг 2: анализ сцены (использует реальную глубину, если она есть; иначе — по яркости).
        var analysis = analyzer.Analyze(inputPath, realDepth);

        // Шаг 3: построение 3D-геометрии из анализа.
        var generationResult = direct3DGenerator.GenerateDirect3DFromAnalysis(analysis, levelId: 1);

        app.Logger.LogInformation("Direct3D generation complete - Success: {Success}", generationResult.Success);
        app.Logger.LogInformation("  AI Analysis: {Objects} objects detected, complexity: {Complexity:P}", 
            generationResult.ObjectsDetected, 
            generationResult.Analysis?.Complexity ?? 0);
        app.Logger.LogInformation("  3D Geometry: {Meshes} meshes, {Vertices} vertices, {Triangles} triangles",
            generationResult.Stats.TotalMeshes,
            generationResult.Stats.TotalVertices,
            generationResult.Stats.TotalTriangles);
        app.Logger.LogInformation("  Scene Volume: {Volume:F2} cubic units", generationResult.Stats.SceneVolume);

        var depthGrid = BuildDepthGrid(analysis?.DepthMap, 128);
        var levelData = analysis != null ? BuildLevelFromAnalysis(analysis) : null;

        return Results.Ok(new
        {
            success = generationResult.Success,
            version = "3.1-Enhanced",
            geometry_stats = new
            {
                total_meshes = generationResult.Stats.TotalMeshes,
                total_vertices = generationResult.Stats.TotalVertices,
                total_triangles = generationResult.Stats.TotalTriangles,
                detected_platforms = generationResult.Stats.DetectedPlatforms,
                scene_volume = generationResult.Stats.SceneVolume
            },
            objects_detected = generationResult.ObjectsDetected,
            quality_level = "Enhanced",
            analysis_source = analysisSource,
            level_2d = levelData,
            platform_count = levelData?.Platforms?.Count ?? 0,
            analysis = new
            {
                detected_objects = analysis?.Objects?.Count ?? 0,
                scene_complexity = analysis?.Complexity ?? 0,
                interest_points = analysis?.InterestPoints?.Count ?? 0,
                depth_grid = new
                {
                    size = depthGrid.size,
                    values = depthGrid.values
                },
                detailed_objects = analysis?.Objects?.Select(o => new
                {
                    o.Id,
                    o.X,
                    o.Y,
                    o.Width,
                    o.Height,
                    o.Type,
                    raw_class = o.RawClass,
                    confidence = o.Confidence,
                    color_r = o.Color.R,
                    color_g = o.Color.G,
                    color_b = o.Color.B,
                    brightness = (float)(o.Color.R * 0.299f + o.Color.G * 0.587f + o.Color.B * 0.114f)
                }).ToList()
            }
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error in direct 3D level generation");
        return Results.Ok(new 
        { 
            success = false,
            version = "3.0-Direct3D",
            error = ex.Message,
            error_type = ex.GetType().Name,
            geometry_stats = new
            {
                total_meshes = 0,
                total_vertices = 0,
                total_triangles = 0,
                detected_platforms = 0,
                scene_volume = 0f
            },
            objects_detected = 0
        });
    }
});

// World Model API (Yan-style): init from image, step simulation, edit structure/style
app.MapPost("/api/world/init", async (HttpRequest request) =>
{
    try
    {
        if (!request.HasFormContentType || request.Form.Files.Count == 0)
        {
            return Results.BadRequest(new { error = "No image file provided" });
        }

        var file = request.Form.Files[0];
        string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(uploadPath);

        string inputPath = Path.Combine(uploadPath, "world_input.png");
        using (var stream = new FileStream(inputPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var analyzer = new AIImageAnalyzer();
        var analysis = analyzer.Analyze(inputPath);
        var state = worldEngine.InitFromAnalysis(analysis);
        worldSessions[state.SessionId] = state;

        return Results.Ok(new
        {
            success = true,
            session_id = state.SessionId,
            world_state = state
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error initializing world model");
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/world/step", async (HttpRequest request) =>
{
    try
    {
        var body = await request.ReadFromJsonAsync<WorldStepRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.SessionId))
        {
            return Results.BadRequest(new { error = "session_id is required" });
        }

        if (!worldSessions.TryGetValue(body.SessionId, out var state))
        {
            return Results.BadRequest(new { error = "Invalid session_id" });
        }

        state = worldEngine.Step(state, body.Action ?? string.Empty);
        worldSessions[body.SessionId] = state;

        return Results.Ok(new
        {
            success = true,
            session_id = body.SessionId,
            world_state = state
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error stepping world model");
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/world/edit", async (HttpRequest request) =>
{
    try
    {
        var body = await request.ReadFromJsonAsync<WorldEditRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.SessionId))
        {
            return Results.BadRequest(new { error = "session_id is required" });
        }

        if (!worldSessions.TryGetValue(body.SessionId, out var state))
        {
            return Results.BadRequest(new { error = "Invalid session_id" });
        }

        state = worldEngine.ApplyEdit(state, body.Edit);
        worldSessions[body.SessionId] = state;

        return Results.Ok(new
        {
            success = true,
            session_id = body.SessionId,
            world_state = state
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error editing world model");
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/world/save", async (HttpRequest request) =>
{
    try
    {
        var body = await request.ReadFromJsonAsync<WorldSessionRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.SessionId))
        {
            return Results.BadRequest(new { error = "session_id is required" });
        }

        if (!worldSessions.TryGetValue(body.SessionId, out var state))
        {
            return Results.BadRequest(new { error = "Invalid session_id" });
        }

        var filePath = Path.Combine(worldStorageDir, $"world_{body.SessionId}.json");
        var json = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(filePath, json);

        return Results.Ok(new
        {
            success = true,
            session_id = body.SessionId,
            file = filePath
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error saving world session");
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/world/load", async (HttpRequest request) =>
{
    try
    {
        var body = await request.ReadFromJsonAsync<WorldSessionRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.SessionId))
        {
            return Results.BadRequest(new { error = "session_id is required" });
        }

        if (worldSessions.TryGetValue(body.SessionId, out var cached))
        {
            return Results.Ok(new { success = true, session_id = body.SessionId, world_state = cached });
        }

        var filePath = Path.Combine(worldStorageDir, $"world_{body.SessionId}.json");
        if (!File.Exists(filePath))
        {
            return Results.BadRequest(new { error = "Session file not found" });
        }

        var json = await File.ReadAllTextAsync(filePath);
        var state = System.Text.Json.JsonSerializer.Deserialize<WorldModelEngine.WorldState>(json);
        if (state == null)
        {
            return Results.BadRequest(new { error = "Failed to load session file" });
        }

        worldSessions[body.SessionId] = state;
        return Results.Ok(new { success = true, session_id = body.SessionId, world_state = state });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error loading world session");
        return Results.BadRequest(new { error = ex.Message });
    }
});

// API endpoint for 3D export (OBJ format) - Direct3D
app.MapPost("/api/export-3d/obj", async (HttpRequest request) =>
{
    try
    {
        if (!request.HasFormContentType || request.Form.Files.Count == 0)
        {
            return Results.BadRequest(new { error = "No image file provided" });
        }

        var file = request.Form.Files[0];
        
        string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(uploadPath);

        string inputPath = Path.Combine(uploadPath, "input_3d.png");
        
        using (var stream = new FileStream(inputPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var direct3DGenerator = new Direct3DLevelGenerator();
        var generationResult = direct3DGenerator.GenerateDirect3D(inputPath, levelId: 1);
        
        if (!generationResult.Success || generationResult.Meshes.Count == 0)
            return Results.BadRequest(new { error = "Failed to generate 3D level" });

        string objContent = Direct3DLevelGenerator.ExportMeshesToOBJ(generationResult.Meshes);
        
        return Results.Ok(new
        {
            success = true,
            format = "OBJ",
            obj_data = objContent,
            statistics = new
            {
                meshes = generationResult.Stats.TotalMeshes,
                vertices = generationResult.Stats.TotalVertices,
                triangles = generationResult.Stats.TotalTriangles,
                platforms = generationResult.Stats.DetectedPlatforms,
                obstacles = generationResult.Stats.DetectedObstacles
            }
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error in 3D OBJ export");
        return Results.Ok(new { success = false, error = ex.Message, version = "3.0-Direct3D" });
    }
});

// API endpoint for 3D export (GLTF format) - Direct3D
app.MapPost("/api/export-3d/gltf", async (HttpRequest request) =>
{
    try
    {
        if (!request.HasFormContentType || request.Form.Files.Count == 0)
        {
            return Results.BadRequest(new { error = "No image file provided" });
        }

        var file = request.Form.Files[0];
        
        string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(uploadPath);

        string inputPath = Path.Combine(uploadPath, "input_gltf.png");
        
        using (var stream = new FileStream(inputPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var direct3DGenerator = new Direct3DLevelGenerator();
        var generationResult = direct3DGenerator.GenerateDirect3D(inputPath, levelId: 1);

        if (!generationResult.Success || generationResult.Meshes.Count == 0)
            return Results.BadRequest(new { error = "Failed to generate 3D level" });

        string gltfContent = Direct3DLevelGenerator.ExportMeshesToGLTF(generationResult.Meshes);

        return Results.Ok(new
        {
            success = true,
            format = "GLTF",
            gltf_data = gltfContent,
            statistics = new
            {
                meshes = generationResult.Stats.TotalMeshes,
                vertices = generationResult.Stats.TotalVertices,
                triangles = generationResult.Stats.TotalTriangles,
                platforms = generationResult.Stats.DetectedPlatforms,
                obstacles = generationResult.Stats.DetectedObstacles
            }
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error in 3D GLTF export");
        return Results.BadRequest(new { error = ex.Message });
    }
});

// API endpoint for full 3D package (all formats) - Direct3D
app.MapPost("/api/export-3d/package", async (HttpRequest request) =>
{
    try
    {
        if (!request.HasFormContentType || request.Form.Files.Count == 0)
        {
            return Results.BadRequest(new { error = "No image file provided" });
        }

        var file = request.Form.Files[0];
        
        string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(uploadPath);

        string inputPath = Path.Combine(uploadPath, "input_package.png");
        
        using (var stream = new FileStream(inputPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var direct3DGenerator = new Direct3DLevelGenerator();
        var generationResult = direct3DGenerator.GenerateDirect3D(inputPath, levelId: 1);

        if (!generationResult.Success || generationResult.Meshes.Count == 0)
            return Results.BadRequest(new { error = "Failed to generate 3D level" });

        // Экспортируем в разные форматы
        var package = new Dictionary<string, string>
        {
            { "level.obj", Direct3DLevelGenerator.ExportMeshesToOBJ(generationResult.Meshes) },
            { "level.gltf", Direct3DLevelGenerator.ExportMeshesToGLTF(generationResult.Meshes) },
            { "manifest.json", System.Text.Json.JsonSerializer.Serialize(generationResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) }
        };

        return Results.Ok(new
        {
            success = true,
            format = "Package",
            files = package.Keys.ToList(),
            package_size_estimate = package.Values.Sum(v => v.Length),
            statistics = new
            {
                meshes = generationResult.Stats.TotalMeshes,
                vertices = generationResult.Stats.TotalVertices,
                triangles = generationResult.Stats.TotalTriangles,
                platforms = generationResult.Stats.DetectedPlatforms,
                obstacles = generationResult.Stats.DetectedObstacles,
                total_files = package.Count
            }
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error in 3D package export");
        return Results.BadRequest(new { error = ex.Message });
    }
});

// API endpoint to save game level
app.MapPost("/api/save-level", async (HttpRequest request) =>
{
    try
    {
        using (var reader = new StreamReader(request.Body))
        {
            var json = await reader.ReadToEndAsync();
            if (string.IsNullOrEmpty(json))
            {
                return Results.BadRequest(new { error = "Empty request body" });
            }
            currentGameLevel = json;
            app.Logger.LogInformation("Game level saved to server memory (length: {Length})", json.Length);
            return Results.Ok(new { success = true, message = "Level saved" });
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error saving level");
        return Results.BadRequest(new { error = ex.Message });
    }
});

// API endpoint to retrieve game level
app.MapGet("/api/get-level", (HttpContext context) =>
{
    if (currentGameLevel == null)
    {
        app.Logger.LogWarning("No level found in server memory");
        return Results.BadRequest(new { error = "No level found. Please generate a level first." });
    }

    app.Logger.LogInformation("Game level retrieved from server memory");
    // Return raw JSON without double serialization
    context.Response.ContentType = "application/json";
    return Results.Text(currentGameLevel, "application/json");
});

app.MapGet("/api/health", () => Results.Ok(new { status = "Online", version = "3.0-Direct3D", timestamp = DateTime.UtcNow }));

// Сохранение рендера канваса (data URL) в файл — для before/after-картинок к диплому.
app.MapPost("/api/save-render", async (HttpRequest request) =>
{
    try
    {
        var body = await request.ReadFromJsonAsync<SaveRenderRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.Name) || string.IsNullOrWhiteSpace(body.DataUrl))
            return Results.BadRequest(new { error = "name and dataUrl required" });

        var comma = body.DataUrl.IndexOf(',');
        var b64 = comma >= 0 ? body.DataUrl[(comma + 1)..] : body.DataUrl;
        var bytes = Convert.FromBase64String(b64);

        var dir = Path.Combine(Directory.GetCurrentDirectory(), "docs", "renders");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, Path.GetFileName(body.Name));
        await File.WriteAllBytesAsync(path, bytes);

        app.Logger.LogInformation("Saved render: {Path} ({Bytes} bytes)", path, bytes.Length);
        return Results.Ok(new { success = true, path, bytes = bytes.Length });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapFallbackToFile("index.html");

app.Logger.LogInformation("Server configured and ready");
await app.RunAsync();

public class SaveRenderRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("dataUrl")]
    public string? DataUrl { get; set; }
}

public class WorldStepRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("action")]
    public string? Action { get; set; }
}

public class WorldEditRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("edit")]
    public WorldModelEngine.WorldEdit? Edit { get; set; }
}

public class WorldSessionRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("session_id")]
    public string? SessionId { get; set; }
}
