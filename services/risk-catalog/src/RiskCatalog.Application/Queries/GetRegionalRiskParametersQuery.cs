using MediatR;
using RiskCatalog.Application.DTO;

namespace RiskCatalog.Application.Queries;

public record GetRegionalRiskParametersQuery : IRequest<RegionalRiskParametersDTO>
{
    public Guid RegionId { get; set; }
}