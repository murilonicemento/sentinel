using Ingestion.Application.DTO;
using MediatR;

namespace Ingestion.Application.Commands;

public record RegisterSensorCollectionCommand(
    Guid DatasourceId,
    DateTime CollectedAt,
    string Payload,
    ICollection<SampleSensorDTO> Samples
) : IRequest<Guid>;