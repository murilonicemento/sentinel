using System.ComponentModel.DataAnnotations;
using Ingestion.Application.Attributes;
using Ingestion.Application.DTO;
using MediatR;

namespace Ingestion.Application.Commands;

public record RegisterClimaticEventCommand : IRequest<Guid>
{
    [Required] public Guid DatasourceId { get; set; }
    [Required] public Guid TenantId { get; set; }
    [Required] [NotInFuture] public DateTime CollectedAt { get; set; }
    [Required] public string Payload { get; set; } = null!;
    [Required] public List<SampleSensorDTO> SampleSensors { get; set; } = [];
}