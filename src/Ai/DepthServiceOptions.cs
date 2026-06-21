/// <summary>
/// Настройки ИИ-сервиса оценки глубины (Depth Anything V2).
/// Читается из appsettings.json -> секция "DepthService".
/// </summary>
public class DepthServiceOptions
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "http://127.0.0.1:8001";
    public int TimeoutSeconds { get; set; } = 60;
}
