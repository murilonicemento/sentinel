namespace Ingestion.Application.DTO;

public class BatchEvaluationDTO
{
    public required string Type { get; set; }
    public required object Payload { get; set; }
}