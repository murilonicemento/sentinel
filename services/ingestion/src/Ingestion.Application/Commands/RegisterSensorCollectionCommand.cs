using System.ComponentModel.DataAnnotations;
using Ingestion.Application.Attributes;
using Ingestion.Application.DTO;
using MediatR;

namespace Ingestion.Application.Commands;

public record RegisterSensorCollectionCommand : IRequest<Guid>
{
    [Required] public Guid DatasourceId { get; set; }
    [Required] public Guid TenantId { get; set; }
    [Required] [FutureDate] public DateTime CollectedAt { get; set; }
    [Required] public string Payload { get; set; } = string.Empty;
    [Required] public IEnumerable<SampleSensorDTO> SampleSensors { get; set; } = [];
}