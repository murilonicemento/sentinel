namespace Ingestion.Application.DTO;

public record BatchResponseDTO
{
    public List<BatchResultDTO> Results { get; set; } = [];
}