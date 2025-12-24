using MediatR;
using RiskCatalog.Application.DTO;

namespace RiskCatalog.Application.Queries;

public record GetIDFCurvesForEventTypeQuery : IRequest<IDFCurvesDTO>
{
    public string EventTypeCode { get; set; }
    public int? ReturnPeriodYears { get; set; }
}