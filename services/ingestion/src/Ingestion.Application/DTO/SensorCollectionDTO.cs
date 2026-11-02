using Ingestion.Domain.Enums;

namespace Ingestion.Application.DTO;

public class SensorCollectionDTO
{
    public Guid DataSourceId { get; init; }
    public Guid TenantId { get; init; }
    public SensorDomainEnum Domain { get; init; }
    public string Payload { get; set; } = string.Empty;
    public DateTime? CollectedAt { get; init; }
    public List<SampleSensorDTO> Samples { get; init; } = [];
}