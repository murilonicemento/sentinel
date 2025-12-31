using MediatR;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Queries;
using RiskCatalog.Domain.IRepositories;

namespace RiskCatalog.Application.Handlers;

public class RiskMatrixForEventTypeHandler : IRequestHandler<GetRiskMatrixForEventTypeQuery, RiskMatrixDTO?>
{
    private readonly IRiskMatrixRepository _riskMatrixRepository;

    public RiskMatrixForEventTypeHandler(IRiskMatrixRepository riskMatrixRepository)
    {
        _riskMatrixRepository = riskMatrixRepository;
    }

    public async Task<RiskMatrixDTO?> Handle(GetRiskMatrixForEventTypeQuery request,
        CancellationToken cancellationToken)
    {
        var riskMatrix = await _riskMatrixRepository.GetRiskMatrixForEventTypeAsync(
            request.EventTypeCode,
            request.SeverityLevel,
            request.Version);

        if (riskMatrix is null)
            return null;

        return new RiskMatrixDTO
        {
            EventTypeCode = riskMatrix.EventType.Code,
            SeverityLevel = riskMatrix.SeverityLevel.ToString(),
            RiskLevel = riskMatrix.RiskLevel.ToString(),
            Version = riskMatrix.Version
        };
    }
}