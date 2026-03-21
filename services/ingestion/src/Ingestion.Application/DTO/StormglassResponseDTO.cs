namespace Ingestion.Application.DTO;

public class StormglassResponseDTO
{
    public List<StormglassHourDTO> Hours { get; set; } = new();
    public MetaDTO Meta { get; set; } = new();
}