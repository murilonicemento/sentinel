namespace Ingestion.Application.DTO;

public class BatchRequestDTO
{
    public List<BatchEvaluationDTO> Evaluations { get; set; } = [];
}