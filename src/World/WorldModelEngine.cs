using System;
using System.Collections.Generic;
using System.Linq;

public class WorldModelEngine
{
    public class WorldState
    {
        public string SessionId { get; set; }
        public int Tick { get; set; }
        public int GridSize { get; set; }
        // Рельеф, выведенный из карты глубины — ОБЩЕЕ ИНВАРИАНТНОЕ СОСТОЯНИЕ мира (как в Yan:
        // глубина связывает структуру и стиль). Задаётся один раз при init и переживает любые
        // правки, поэтому смена стиля сохраняет геометрию и физику, а мир «помнит» себя.
        public List<float> BaseTerrain { get; set; } = new();
        public List<float> TerrainHeights { get; set; } = new();
        public List<WorldObject> Objects { get; set; } = new();
        public CameraState Camera { get; set; } = new();
        public WorldStyle Style { get; set; } = new();
    }

    public class WorldObject
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float Depth { get; set; }
        public int ColorR { get; set; }
        public int ColorG { get; set; }
        public int ColorB { get; set; }
        public float Brightness { get; set; }
        public float VelX { get; set; }
        public float VelY { get; set; }
    }

    public class CameraState
    {
        public float X { get; set; } = 0;
        public float Y { get; set; } = 240;
        public float Z { get; set; } = 360;
        public float Yaw { get; set; } = 0;
        public float Pitch { get; set; } = -0.25f;
    }

    public class WorldStyle
    {
        public string Mode { get; set; } = "neutral";
        public int TintR { get; set; } = 255;
        public int TintG { get; set; } = 255;
        public int TintB { get; set; } = 255;
    }

    public WorldState InitFromAnalysis(AIImageAnalyzer.SceneAnalysis analysis)
    {
        var state = new WorldState
        {
            SessionId = Guid.NewGuid().ToString("N"),
            Tick = 0,
            GridSize = 48,
            Camera = new CameraState(),
            Style = new WorldStyle()
        };

        if (analysis?.Objects == null || analysis.Objects.Count == 0)
        {
            return state;
        }

        var minX = analysis.Objects.Min(o => o.X);
        var maxX = analysis.Objects.Max(o => o.X + o.Width);
        var minY = analysis.Objects.Min(o => o.Y);
        var maxY = analysis.Objects.Max(o => o.Y + o.Height);
        var spanX = Math.Max(1, maxX - minX);
        var spanY = Math.Max(1, maxY - minY);

        int nextId = 1;
        foreach (var obj in analysis.Objects)
        {
            var nx = (obj.X - minX) / (float)spanX;
            var ny = (obj.Y - minY) / (float)spanY;
            var nw = Math.Max(0.02f, obj.Width / (float)spanX);
            var nh = Math.Max(0.02f, obj.Height / (float)spanY);

            state.Objects.Add(new WorldObject
            {
                Id = nextId++,
                Type = obj.Type,
                X = nx,
                Y = ny,
                Width = nw,
                Height = nh,
                Depth = Math.Max(0.05f, nh * 0.6f),
                ColorR = obj.Color.R,
                ColorG = obj.Color.G,
                ColorB = obj.Color.B,
                Brightness = (obj.Color.R * 0.299f + obj.Color.G * 0.587f + obj.Color.B * 0.114f),
                VelX = (obj.Type == "enemy" ? 0.0015f : 0f),
                VelY = 0f
            });
        }

        if (analysis?.DepthMap != null && analysis.DepthMap.Width > 0 && analysis.DepthMap.Height > 0)
        {
            // Настоящая глубина от нейросети → инвариантная база геометрии мира.
            state.BaseTerrain = BuildTerrainGridFromDepthMap(analysis.DepthMap, state.GridSize);
            state.TerrainHeights = new List<float>(state.BaseTerrain);
        }
        else
        {
            // Глубины нет — реального инварианта геометрии нет, рельеф выводим из объектов.
            state.BaseTerrain = new List<float>();
            state.TerrainHeights = BuildTerrainGrid(state.Objects, state.GridSize);
        }
        return state;
    }

    public WorldState Step(WorldState state, string? action)
    {
        state.Tick += 1;

        foreach (var obj in state.Objects)
        {
            if (obj.Type == "enemy")
            {
                obj.X += obj.VelX;
                if (obj.X > 1f) obj.X = 0.02f;
                if (obj.X < 0f) obj.X = 0.98f;
            }
        }

        const float moveStep = 18f;
        const float turnStep = 0.12f;
        switch ((action ?? string.Empty).ToLowerInvariant())
        {
            case "move_forward":
                state.Camera.Z -= moveStep;
                break;
            case "move_back":
                state.Camera.Z += moveStep;
                break;
            case "move_left":
                state.Camera.X -= moveStep;
                break;
            case "move_right":
                state.Camera.X += moveStep;
                break;
            case "turn_left":
                state.Camera.Yaw -= turnStep;
                break;
            case "turn_right":
                state.Camera.Yaw += turnStep;
                break;
        }

        return state;
    }

    public WorldState ApplyEdit(WorldState state, WorldEdit? edit)
    {
        if (edit == null) return state;

        if (edit.EditType == "add_object")
        {
            var id = state.Objects.Count > 0 ? state.Objects.Max(o => o.Id) + 1 : 1;
            state.Objects.Add(new WorldObject
            {
                Id = id,
                Type = string.IsNullOrWhiteSpace(edit.ObjectType) ? "platform" : edit.ObjectType,
                X = Math.Clamp(edit.X, 0.02f, 0.98f),
                Y = Math.Clamp(edit.Y, 0.02f, 0.98f),
                Width = Math.Clamp(edit.Width, 0.03f, 0.2f),
                Height = Math.Clamp(edit.Height, 0.03f, 0.2f),
                Depth = Math.Clamp(edit.Height * 0.6f, 0.05f, 0.2f),
                ColorR = 200,
                ColorG = 210,
                ColorB = 220,
                Brightness = 190
            });
        }
        else if (edit.EditType == "style")
        {
            ApplyStyle(state, edit.Style);
        }

        // Глубина — общее инвариантное состояние: пока есть depth-база, ЛЮБАЯ правка (в т.ч.
        // стилевая) сохраняет геометрию. Перестраиваем рельеф из объектов только в фолбэке,
        // когда настоящей глубины нет.
        bool hasDepthBase = state.BaseTerrain != null
            && state.BaseTerrain.Count == state.GridSize * state.GridSize;
        state.TerrainHeights = hasDepthBase
            ? new List<float>(state.BaseTerrain)
            : BuildTerrainGrid(state.Objects, state.GridSize);
        return state;
    }

    private void ApplyStyle(WorldState state, string style)
    {
        var mode = (style ?? "neutral").ToLowerInvariant();
        state.Style.Mode = mode;

        switch (mode)
        {
            case "warm":
                state.Style.TintR = 255;
                state.Style.TintG = 225;
                state.Style.TintB = 200;
                break;
            case "cool":
                state.Style.TintR = 200;
                state.Style.TintG = 225;
                state.Style.TintB = 255;
                break;
            case "mono":
                state.Style.TintR = 215;
                state.Style.TintG = 215;
                state.Style.TintB = 215;
                break;
            default:
                state.Style.TintR = 255;
                state.Style.TintG = 255;
                state.Style.TintB = 255;
                break;
        }
    }

    private List<float> BuildTerrainGrid(List<WorldObject> objects, int gridSize)
    {
        var heights = new float[gridSize * gridSize];
        var counts = new int[gridSize * gridSize];

        int Index(int gx, int gy) => gy * gridSize + gx;

        foreach (var obj in objects)
        {
            var x0 = Math.Clamp(obj.X, 0f, 1f);
            var y0 = Math.Clamp(obj.Y, 0f, 1f);
            var x1 = Math.Clamp(obj.X + obj.Width, 0f, 1f);
            var y1 = Math.Clamp(obj.Y + obj.Height, 0f, 1f);

            int gx0 = Math.Clamp((int)(x0 * (gridSize - 1)), 0, gridSize - 1);
            int gx1 = Math.Clamp((int)(x1 * (gridSize - 1)), 0, gridSize - 1);
            int gy0 = Math.Clamp((int)(y0 * (gridSize - 1)), 0, gridSize - 1);
            int gy1 = Math.Clamp((int)(y1 * (gridSize - 1)), 0, gridSize - 1);

            float baseHeight = 0.25f + (obj.Brightness / 255f) * 0.55f;

            for (int gy = gy0; gy <= gy1; gy++)
            {
                for (int gx = gx0; gx <= gx1; gx++)
                {
                    int idx = Index(gx, gy);
                    heights[idx] += baseHeight;
                    counts[idx] += 1;
                }
            }
        }

        var result = new List<float>(gridSize * gridSize);
        for (int i = 0; i < heights.Length; i++)
        {
            if (counts[i] > 0)
            {
                result.Add(heights[i] / counts[i]);
            }
            else
            {
                result.Add(0f);
            }
        }

        return result;
    }

    private List<float> BuildTerrainGridFromDepthMap(AIImageAnalyzer.DepthMap depthMap, int gridSize)
    {
        var heights = new float[gridSize * gridSize];
        var counts = new int[gridSize * gridSize];

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
                int sampleCount = 0;

                for (int y = y0; y < y1; y++)
                {
                    for (int x = x0; x < x1; x++)
                    {
                        sum += depthMap.Values[y, x];
                        sampleCount++;
                    }
                }

                int idx = Index(gx, gy);
                if (sampleCount > 0)
                {
                    heights[idx] = sum / sampleCount;
                    counts[idx] = sampleCount;
                }
            }
        }

        // Light smoothing pass to preserve structure but reduce noise.
        var smoothed = new float[gridSize * gridSize];
        for (int gy = 0; gy < gridSize; gy++)
        {
            for (int gx = 0; gx < gridSize; gx++)
            {
                float total = 0f;
                float weight = 0f;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = gx + dx;
                        int ny = gy + dy;
                        if (nx < 0 || ny < 0 || nx >= gridSize || ny >= gridSize) continue;
                        float w = (dx == 0 && dy == 0) ? 3f : 1f;
                        total += heights[Index(nx, ny)] * w;
                        weight += w;
                    }
                }
                smoothed[Index(gx, gy)] = weight > 0 ? total / weight : heights[Index(gx, gy)];
            }
        }

        var result = new List<float>(gridSize * gridSize);
        for (int i = 0; i < smoothed.Length; i++)
        {
            result.Add(Math.Clamp(smoothed[i], 0f, 1f));
        }

        return result;
    }

    public class WorldEdit
    {
        public string EditType { get; set; }
        public string ObjectType { get; set; }
        public float X { get; set; } = 0.5f;
        public float Y { get; set; } = 0.5f;
        public float Width { get; set; } = 0.08f;
        public float Height { get; set; } = 0.08f;
        public string Style { get; set; } = "neutral";
    }
}
