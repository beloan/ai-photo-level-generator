using System.Collections.Generic;

public struct Vector2D
{
    public int X { get; set; }
    public int Y { get; set; }

    public Vector2D(int x, int y)
    {
        X = x;
        Y = y;
    }
}

public struct PlatformData
{
    public Vector2D Start { get; set; }
    public Vector2D End { get; set; }
    public int Height { get; set; }

    public PlatformData(Vector2D start, Vector2D end, int height = 20)
    {
        Start = start;
        End = end;
        Height = height;
    }
}

public class LevelDataContainer
{
    public int Width { get; set; }
    public int Height { get; set; }
    public List<PlatformData> Platforms { get; set; }

    public LevelDataContainer()
    {
        Platforms = new List<PlatformData>();
    }
}
