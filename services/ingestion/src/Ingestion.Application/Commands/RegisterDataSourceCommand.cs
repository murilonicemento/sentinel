using System.ComponentModel.DataAnnotations;
using Ingestion.Domain.ValueObjects;
using MediatR;

namespace Ingestion.Application.Commands;

public record RegisterDataSourceCommand : IRequest<Guid>
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string DataSourceType { get; set; } = string.Empty;
    [Required] public string MeasurementType { get; set; } = string.Empty;
    [Required] public string Endpoint { get; set; } = string.Empty;
    [Required] public string CollectionFrequency { get; set; } = string.Empty;
}