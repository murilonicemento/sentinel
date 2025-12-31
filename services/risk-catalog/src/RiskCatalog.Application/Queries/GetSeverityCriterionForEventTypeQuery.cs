using MediatR;
using RiskCatalog.Application.DTO;

namespace RiskCatalog.Application.Queries;

public record GetSeverityCriterionForEventTypeQuery : IRequest<List<SeverityCriterionDTO>>
{
    public string EventTypeCode { get; set; }
    public int? Version { get; set; }
}