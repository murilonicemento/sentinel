using Ingestion.Domain.Enums;

namespace Ingestion.Application.DTO;

public class DisasterCollectionDTO : SensorCollectionDTO
{
    public DisasterEventEnum? DisasterType { get; init; }
}