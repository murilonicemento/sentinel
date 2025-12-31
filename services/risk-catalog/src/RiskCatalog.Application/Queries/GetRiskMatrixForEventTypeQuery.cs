using MediatR;
using RiskCatalog.Application.DTO;

namespace RiskCatalog.Application.Queries;

public record GetRiskMatrixForEventTypeQuery : IRequest<RiskMatrixDTO?>
{
    public string EventTypeCode { get; set; }
    public string SeverityLevel { get; set; }
    public int? Version { get; set; }
}