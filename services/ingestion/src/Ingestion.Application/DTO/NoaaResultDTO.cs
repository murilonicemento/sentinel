namespace Ingestion.Application.DTO;

public class NoaaResultDTO
{
    public DateTime Date { get; set; }
    public string DataType { get; set; } = string.Empty;
    public string Station { get; set; } = string.Empty;
    public string Attributes { get; set; } = string.Empty;
    public double Value { get; set; }
}