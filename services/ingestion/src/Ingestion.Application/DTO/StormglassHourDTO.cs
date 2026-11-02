namespace Ingestion.Application.DTO;

public class StormglassHourDTO
{
    public Dictionary<string, double> Gust { get; set; } = new();
    public Dictionary<string, double> Precipitation { get; set; } = new();
    public Dictionary<string, double> Pressure { get; set; } = new();
    public DateTime Time { get; set; }
}