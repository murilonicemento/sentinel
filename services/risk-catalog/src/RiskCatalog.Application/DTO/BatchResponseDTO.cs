namespace RiskCatalog.Application.DTO;

public record BatchResponseDTO
{
    public List<BatchResultDTO> Results { get; set; } = [];
}