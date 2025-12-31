using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Queries;

namespace RiskCatalog.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RiskModelController : ControllerBase
{
    private readonly IMediator _mediator;

    public RiskModelController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("risk-matrix")]
    public async Task<ActionResult<RiskMatrixDTO>> GetRiskMatrixForEventType(
        [FromQuery] string eventTypeCode,
        [FromQuery] string severityLevel,
        [FromQuery] int? version
    )
    {
        var query = new GetRiskMatrixForEventTypeQuery
        {
            EventTypeCode = eventTypeCode,
            SeverityLevel = severityLevel,
            Version = version
        };
        var riskMatrix = await _mediator.Send(query);

        return riskMatrix is not null ? Ok(riskMatrix) : NoContent();
    }

    [HttpGet("idf-curves")]
    public async Task<ActionResult<List<IDFCurvesDTO>>> GetIDFCurvesForEventType(
        [FromQuery] string eventTypeCode,
        [FromQuery] int? returnPeriodYears
    )
    {
        var query = new GetIDFCurvesForEventTypeQuery
        {
            EventTypeCode = eventTypeCode,
            ReturnPeriodYears = returnPeriodYears
        };
        var idfCurves = await _mediator.Send(query);

        return idfCurves is not null ? Ok(idfCurves) : NoContent();
    }

    [HttpGet("regional-risk-parameters")]
    public async Task<ActionResult<RegionalRiskParametersDTO>> GetRegionalRiskParameters([FromQuery] Guid regionId)
    {
        var query = new GetRegionalRiskParametersQuery
        {
            RegionId = regionId
        };
        var regionParameter = await _mediator.Send(query);

        return regionParameter is not null ? Ok(regionParameter) : NoContent();
    }
}