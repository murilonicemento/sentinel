using System.ComponentModel.DataAnnotations;
using Ingestion.Application.Attributes;
using Ingestion.Application.DTO;
using MediatR;

namespace Ingestion.Application.Commands;

public record RegisterSensorCollectionCommand : IRequest<Guid>
{
    [Required] public Guid DatasourceId { get; }
    [Required] public Guid TenantId { get; }
    [Required] [FutureDate] public DateTime CollectedAt { get; }
    [Required] public string Payload { get; }
    [Required] public IEnumerable<SampleSensorDTO> SampleSensors { get; } = [];
}