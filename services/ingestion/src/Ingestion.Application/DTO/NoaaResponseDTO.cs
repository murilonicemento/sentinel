namespace Ingestion.Application.DTO;

public class NoaaResponseDTO
{
    public MetadataDTO Metadata { get; set; } = new();
    public List<NoaaResultDTO> Results { get; set; } = new();
}