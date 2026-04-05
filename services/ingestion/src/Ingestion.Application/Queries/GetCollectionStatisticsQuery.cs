using Ingestion.Application.DTO;
using MediatR;

namespace Ingestion.Application.Queries;

public record GetCollectionStatisticsQuery : IRequest<CollectionStatisticsResponseDTO>
{
    public DateTime? InitialDate { get; set; }
    public DateTime? EndDate { get; set; }
}