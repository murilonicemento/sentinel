using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Services;
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
        var isCreated = await _riskModelService.CreateRiskMatrixAsync(riskMatrixDto);

        return isCreated
            ? Created("/api/risk-model/risk-matrix", new { isCreated })
            : Problem(
                title: "An error occurred.",
                detail: "Failed to create Risk Matrix.",
                type: "AddError",
                statusCode: 500
            );
    }

    [HttpPost("idf-curves")]
    [Authorize(Policy = "RiskCatalogWrite")]
    public async Task<ActionResult<IDFCurvesDTO>> CreateIDFCurves([FromBody] CreateIDFCurvesDTO idfCurvesDto)
    {
        var isCreated = await _riskModelService.CreateIDFCurvesAsync(idfCurvesDto);

        return isCreated
            ? Created("/api/risk-model/idf-curves", new { isCreated })
            : Problem(
                title: "An error occurred.",
                detail: "Failed to create IDF Curves.",
                type: "AddError",
                statusCode: 500
            );
    }

    [HttpPost("regional-risk-parameters")]
    [Authorize(Policy = "RiskCatalogWrite")]
    public async Task<ActionResult> CreateRegionalRiskParametersToRegion(
        [FromBody] CreateRegionalRiskParameterDTO regionalRiskParametersDto)
    {
        var isCreated = await _riskModelService.CreateRegionalRiskParametersAsync(regionalRiskParametersDto);

        return isCreated
            ? Created("/api/risk-model/regional-risk-parameters", new { isCreated })
            : Problem(
                title: "An error occurred.",
                detail: "Failed to create Regional Risk Parameters.",
                type: "AddError",
                statusCode: 500
            );
    }

    [HttpPost("catalog/publish")]
    [Authorize(Policy = "RiskCatalogWrite")]
    public async Task<ActionResult> PublishRiskCatalogVersion([FromBody] CatalogPublishDTO catalogPublishDto)
    {
        var tenantId = Guid.Parse(HttpContext.User.FindFirst("tenantId")?.Value!);
        var isPublished = await _riskModelService.PublishCatalogVersionAsync(catalogPublishDto, tenantId);

        return isPublished
            ? Ok(new { message = "Catalog version published successfully", version = catalogPublishDto.Version })
            : Problem(
                title: "An error occurred.",
                detail: "Failed to publish catalog version.",
                type: "PublishError",
                statusCode: 500
            );
    }
}