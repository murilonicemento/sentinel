using Ingestion.Application.DTO;
using Ingestion.Application.Events;
using MediatR;

namespace Ingestion.Application.Queries;

public record GetLatestDetectedEventsQuery : IRequest<IEnumerable<ClimaticEventDetectedEvent>>
{
    public int Limit { get; set; }
}