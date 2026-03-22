namespace Ingestion.Application.DTO;

public class BatchRequestDTO
{
    public List<BatchEvaluationDTO> Evaluations { get; set; } = [];
}

public class BatchEvaluationDTO
{
    public string Type { get; set; }
    public object Payload { get; set; }
}