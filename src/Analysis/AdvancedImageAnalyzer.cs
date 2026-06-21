using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Generic;
using System.Linq;

public enum PlatformType
{
    Normal = 0,
    Dangerous = 1,  // Red/dark platforms
    Collectible = 2, // Yellow/bright platforms
    Moving = 3      // Animated platforms
}

public struct AdvancedPlatformData
{
    public Vector2D Start { get; set; }
    public Vector2D End { get; set; }
    public int Height { get; set; }
    public PlatformType Type { get; set; }

    public AdvancedPlatformData(Vector2D start, Vector2D end, int height = 20, PlatformType type = PlatformType.Normal)
    {
        Start = start;
        End = end;
        Height = height;
        Type = type;
    }
}

public class AdvancedImageAnalyzer
{
    private const int MIN_PLATFORM_LENGTH = 20;
    private const int VERTICAL_MERGE_THRESHOLD = 3;

    public LevelDataContainer AnalyzeWithAdvancedVision(string imagePath)
    {
        var levelData = new LevelDataContainer();
        using (Image<Rgba32> image = Image.Load<Rgba32>(imagePath))
        {
            levelData.Width = image.Width;
            levelData.Height = image.Height;

            // Extract platforms by finding dark pixels
            var rawPlatforms = ExtractRawPlatforms(image);
            
            // Merge nearby platforms
            var mergedPlatforms = MergePlatforms(rawPlatforms);
            
            // Filter by minimum length
            var filteredPlatforms = mergedPlatforms
                .Where(p => p.End.X - p.Start.X >= MIN_PLATFORM_LENGTH)
                .ToList();

            // Convert to standard format
            levelData.Platforms = filteredPlatforms
                .Select(p => new PlatformData(p.Start, p.End, 20))
                .ToList();
        }
        return levelData;
    }

    private List<AdvancedPlatformData> ExtractRawPlatforms(Image<Rgba32> image)
    {
        var platforms = new List<AdvancedPlatformData>();

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                
                // Skip white/light pixels (background)
                if (pixel.R > 200 && pixel.G > 200 && pixel.B > 200)
                    continue;

                // Find horizontal extent of this dark region
                int startX = x;
                var platformType = ClassifyPixelColor(pixel);

                while (x < image.Width)
                {
                    var currentPixel = image[x, y];
                    if (currentPixel.R > 200 && currentPixel.G > 200 && currentPixel.B > 200)
                        break;

                    x++;
                }

                int endX = x - 1;

                if (endX - startX > 5)
                {
                    platforms.Add(new AdvancedPlatformData(
                        new Vector2D(startX, y),
                        new Vector2D(endX, y),
                        20,
                        platformType
                    ));
                }
            }
        }

        return platforms;
    }

    private PlatformType ClassifyPixelColor(Rgba32 pixel)
    {
        // Red channel dominant -> Dangerous
        if (pixel.R > 150 && pixel.G < 100 && pixel.B < 100)
            return PlatformType.Dangerous;
        
        // Yellow (Red + Green) -> Collectible
        if (pixel.R > 150 && pixel.G > 150 && pixel.B < 100)
            return PlatformType.Collectible;
        
        // Blue -> Moving
        if (pixel.R < 100 && pixel.G < 100 && pixel.B > 150)
            return PlatformType.Moving;
        
        // Default: Normal (black, dark gray, etc)
        return PlatformType.Normal;
    }

    private List<AdvancedPlatformData> MergePlatforms(List<AdvancedPlatformData> rawPlatforms)
    {
        if (rawPlatforms.Count == 0)
            return new List<AdvancedPlatformData>();

        var merged = new List<AdvancedPlatformData>();
        var sorted = rawPlatforms
            .OrderBy(p => p.Start.Y)
            .ThenBy(p => p.Start.X)
            .ToList();

        var currentGroup = new List<AdvancedPlatformData> { sorted[0] };

        for (int i = 1; i < sorted.Count; i++)
        {
            var current = sorted[i];
            var lastInGroup = currentGroup.Last();

            // Merge if close vertically, overlapping horizontally, and same type
            if (current.Start.Y - lastInGroup.Start.Y <= VERTICAL_MERGE_THRESHOLD &&
                DoesRangeOverlap(lastInGroup.Start.X, lastInGroup.End.X, current.Start.X, current.End.X) &&
                current.Type == lastInGroup.Type)
            {
                currentGroup.Add(current);
            }
            else
            {
                if (currentGroup.Count > 0)
                {
                    merged.Add(CreateMergedAdvancedPlatform(currentGroup));
                }
                currentGroup = new List<AdvancedPlatformData> { current };
            }
        }

        if (currentGroup.Count > 0)
        {
            merged.Add(CreateMergedAdvancedPlatform(currentGroup));
        }

        return merged;
    }

    private bool DoesRangeOverlap(int start1, int end1, int start2, int end2)
    {
        return !(end1 < start2 || end2 < start1);
    }

    private AdvancedPlatformData CreateMergedAdvancedPlatform(List<AdvancedPlatformData> group)
    {
        int minX = group.Min(p => p.Start.X);
        int maxX = group.Max(p => p.End.X);
        int y = group.First().Start.Y;
        var type = group.First().Type;

        return new AdvancedPlatformData(
            new Vector2D(minX, y),
            new Vector2D(maxX, y),
            20,
            type
        );
    }
}
