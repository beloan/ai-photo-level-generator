using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

/// <summary>
/// Клиент к Python-сервису Depth Anything V2 (ai_service).
/// Отправляет фото, получает карту глубины и строит из неё AIImageAnalyzer.DepthMap
/// в полном разрешении изображения (готова к использованию в анализе/генерации).
/// </summary>
public class DepthEstimationClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DepthEstimationClient(string baseUrl, TimeSpan timeout)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient { Timeout = timeout };
    }

    /// <summary>Возвращает карту глубины (значения 0..1, где 1 — ближе) в разрешении изображения.</summary>
    public async Task<AIImageAnalyzer.DepthMap> EstimateDepthAsync(string imagePath)
    {
        using var form = new MultipartFormDataContent();
        await using var fileStream = File.OpenRead(imagePath);
        form.Add(new StreamContent(fileStream), "file", Path.GetFileName(imagePath));

        using var response = await _httpClient.PostAsync($"{_baseUrl}/depth", form);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var dto = JsonSerializer.Deserialize<DepthResponse>(json, _jsonOptions);
        if (dto == null || dto.Depth == null || dto.Depth.Count == 0)
            throw new InvalidOperationException("Сервис глубины вернул пустой ответ");

        return BuildDepthMap(dto);
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync($"{_baseUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static AIImageAnalyzer.DepthMap BuildDepthMap(DepthResponse dto)
    {
        int width = Math.Max(1, dto.Width);
        int height = Math.Max(1, dto.Height);
        int gridW = Math.Max(1, dto.GridW);
        int gridH = Math.Max(1, dto.GridH);

        var values = new float[height, width];
        for (int y = 0; y < height; y++)
        {
            int gy = Math.Min(gridH - 1, (int)((long)y * gridH / height));
            for (int x = 0; x < width; x++)
            {
                int gx = Math.Min(gridW - 1, (int)((long)x * gridW / width));
                values[y, x] = dto.Depth[gy * gridW + gx];
            }
        }

        return new AIImageAnalyzer.DepthMap { Width = width, Height = height, Values = values };
    }
}

public class DepthResponse
{
    public string Model { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }

    [JsonPropertyName("grid_w")]
    public int GridW { get; set; }

    [JsonPropertyName("grid_h")]
    public int GridH { get; set; }

    public List<float> Depth { get; set; } = new();
}
