using MediatR;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Queries;
using RiskCatalog.Domain.IRepositories;

namespace RiskCatalog.Application.Handlers;

public class RegionalRiskParameterHandler : IRequestHandler<GetRegionalRiskParametersQuery, RegionalRiskParametersDTO?>
{
    private readonly IRegionalParameterRepository _regionalParameterRepository;

    public RegionalRiskParameterHandler(IRegionalParameterRepository regionalParameterRepository)
    {
        _regionalParameterRepository = regionalParameterRepository;
    }

    public async Task<RegionalRiskParametersDTO?> Handle(GetRegionalRiskParametersQuery request,
        CancellationToken cancellationToken)
    {
        var regionalParameter =
            await _regionalParameterRepository.GetByRegionIdAsync(request.RegionId);

        if (regionalParameter is null)
            return null;

        var regionalRiskParametersDTO = new RegionalRiskParametersDTO
        {
            RegionId = regionalParameter.RegionId,
            AdjustmentFactor = regionalParameter.AdjustmentFactor,
            Description = regionalParameter.Description
        };

        return regionalRiskParametersDTO;
    }
}