namespace Ingestion.Application.DTO;

public record RegisterDatasourceResponseDTO
{
    public Guid DataSourceId { get; set; }
    public Guid TenantId { get; set; }
}