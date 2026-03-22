using Ingestion.Domain.Enums;

namespace Ingestion.Application.DTO;

public class ClimaticCollectionDTO : SensorCollectionDTO
{
    public ClimaticEventEnum? ClimaticType { get; init; }
}