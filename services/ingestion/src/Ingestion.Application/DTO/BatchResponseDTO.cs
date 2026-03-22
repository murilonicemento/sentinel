namespace Ingestion.Application.DTO;

public record BatchResponseDTO
{
    public List<BatchResultDTO> Results { get; set; } = [];
}

public class BatchResultDTO
{
    public string Type { get; set; }
    public bool? Contains { get; set; }
    public bool? WithinRadius { get; set; }
    public double? Distance { get; set; }
}