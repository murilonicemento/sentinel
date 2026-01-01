using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Services.Interfaces;

namespace RiskCatalog.Api.Controllers;

[Route("api/risk-model")]
[ApiController]
public class RiskModelController : ControllerBase
{
    private readonly IRiskModelService _riskModelService;

    public RiskModelController(IRiskModelService riskModelService)
    {
        _riskModelService = riskModelService;
    }

    [HttpGet("risk-matrix")]
    public async Task<ActionResult<RiskMatrixDTO>> GetRiskMatrixForEventType(
        [FromQuery] string eventTypeCode,
        [FromQuery] string severityLevel,
        [FromQuery] int? version
    )
    {
        var riskMatrix = await _riskModelService.GetRiskMatrixForEventTypeAsync(
            eventTypeCode,
            severityLevel,
            version);

        return riskMatrix is not null ? Ok(riskMatrix) : NoContent();
    }

    [HttpGet("idf-curves")]
    public async Task<ActionResult<List<IDFCurvesDTO>>> GetIDFCurvesForEventType(
        [FromQuery] string eventTypeCode,
        [FromQuery] int? returnPeriodYears
    )
    {
        var idfCurves = await _riskModelService.GetIDFCurvesForEventTypeAsync(
            eventTypeCode,
            returnPeriodYears);

        return idfCurves is not null ? Ok(idfCurves) : NoContent();
    }

    [HttpGet("regional-risk-parameters")]
    public async Task<ActionResult<RegionalRiskParametersDTO>> GetRegionalRiskParameters([FromQuery] Guid regionId)
    {
        var regionParameter = await _riskModelService.GetRegionalRiskParametersAsync(regionId);

        return regionParameter is not null ? Ok(regionParameter) : NoContent();
    }

    [HttpPost("risk-matrix")]
    [Authorize(Policy = "RiskCatalogWrite")]
    public async Task<ActionResult<RiskMatrixDTO>> CreateRiskMatrix([FromBody] RiskMatrixDTO riskMatrixDto)
    {
        var createdRiskMatrix = await _riskModelService.CreateRiskMatrixAsync(riskMatrixDto);

        return Created("/api/risk-model/risk-matrix", new { eventTypeCode = createdRiskMatrix.EventTypeCode });
    }

    [HttpPost("idf-curves")]
    [Authorize(Policy = "RiskCatalogWrite")]
    public async Task<ActionResult<IDFCurvesDTO>> CreateIDFCurves([FromBody] CreateIDFCurvesDTO idfCurvesDto)
    {
        var createdIDFCurves = await _riskModelService.CreateIDFCurvesAsync(idfCurvesDto);

        return Created("/api/risk-model/idf-curves", new { eventTypeCode = createdIDFCurves.EventTypeCode });
    }

    [HttpPost("regional-risk-parameters")]
    [Authorize(Policy = "RiskCatalogWrite")]
    public async Task<ActionResult> CreateRegionalRiskParametersToRegion(
        [FromBody] RegionalRiskParametersDTO regionalRiskParametersDto)
    {
        return NoContent();
    }

    [HttpPost("catalog/publish")]
    [Authorize(Policy = "RiskCatalogWrite")]
    public async Task<ActionResult> PublishRiskCatalogVersion(CatalogPublishDTO catalogPublishDto)
    {
        return NoContent();
    }
}