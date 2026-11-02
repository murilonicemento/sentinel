namespace Ingestion.Application.DTO;

public class MetaDTO
{
    public int Cost { get; set; }
    public int DailyQuota { get; set; }
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public List<string> Params { get; set; } = new();
    public int RequestCount { get; set; }
}