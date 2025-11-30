namespace Ingestion.Application.DTO;

public record CollectionStatisticsResponse
{
    public int TotalEvents { get; set; }
    public Dictionary<string, int> TotalByType { get; set; } = new();
    public double MinIntensity { get; set; }
    public double MaxIntensity { get; set; }
    public double AverageIntensity { get; set; }
}