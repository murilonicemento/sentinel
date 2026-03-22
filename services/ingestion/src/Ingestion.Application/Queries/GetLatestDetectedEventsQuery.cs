using Ingestion.Application.DTO;
using Ingestion.Application.Events;
using MediatR;

namespace Ingestion.Application.Queries;

public record GetLatestDetectedEventsQuery : IRequest<IEnumerable<SensorEventDetected>>
{
    public int Limit { get; set; }
}