using MediatR;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Queries;
using RiskCatalog.Domain.IRepositories;

namespace RiskCatalog.Application.Handlers;

public class IDFCurvesForEventTypeHandler : IRequestHandler<GetIDFCurvesForEventTypeQuery, IDFCurvesDTO?>
{
    private readonly IIDFCurveRepository _idfCurveRepository;

    public IDFCurvesForEventTypeHandler(IIDFCurveRepository idfCurveRepository)
    {
        _idfCurveRepository = idfCurveRepository;
    }

    public async Task<IDFCurvesDTO?> Handle(GetIDFCurvesForEventTypeQuery request, CancellationToken cancellationToken)
    {
        var IDFCurve =
            await _idfCurveRepository.GetIDFCurveForEventTypeAsync(request.EventTypeCode, request.ReturnPeriodYears);

        if (IDFCurve == null)
            return null;

        var IDFCurveDTO = new IDFCurvesDTO
        {
            DurationMinutes = IDFCurve.DurationMinutes,
            Intensity = IDFCurve.Intensity,
            ReturnPeriodYears = IDFCurve.ReturnPeriodYears,
            Version = IDFCurve.Version,
        };

        return IDFCurveDTO;
    }
}