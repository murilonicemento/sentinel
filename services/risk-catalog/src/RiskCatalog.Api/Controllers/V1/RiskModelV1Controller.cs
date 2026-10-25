using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiskCatalog.Api.Contracts;
using RiskCatalog.Api.Security;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Services.Interfaces;
using static RiskCatalog.Api.Controllers.V1.CatalogV1Responses;

namespace RiskCatalog.Api.Controllers.V1;

public sealed record CatalogPublicationDTO(int Version);

[ApiController]
[Route(BasePath)]
public class RiskModelV1Controller : ControllerBase
{
    private readonly IRiskModelService _riskModelService;

    public RiskModelV1Controller(IRiskModelService riskModelService)
    {
        _riskModelService = riskModelService;
    }

    [HttpGet("risk-matrices")]
    [Authorize(Policy = RiskCatalogPolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<RiskMatrixDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetRiskMatrix([FromQuery] string? eventTypeCode, [FromQuery] string? severityLevel,
        [FromQuery] int? version, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(eventTypeCode)) errors["eventTypeCode"] = ["The eventTypeCode is required."];
        if (string.IsNullOrWhiteSpace(severityLevel)) errors["severityLevel"] = ["The severityLevel is required."];
        if (errors.Count > 0) return Invalid(HttpContext, errors);

        var matrix = await _riskModelService.GetRiskMatrixForEventTypeAsync(eventTypeCode!, severityLevel!, version,
            cancellationToken);
        return matrix is null
            ? NotFoundProblem(HttpContext, "RISK_MATRIX_NOT_FOUND", "Risk matrix not found.")
            : Ok(Envelope(StatusCodes.Status200OK, matrix));
    }

    [HttpGet("idf-curves")]
    [Authorize(Policy = RiskCatalogPolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<IDFCurvesDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetIdfCurve([FromQuery] string? eventTypeCode, [FromQuery] int? returnPeriodYears,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(eventTypeCode))
            return Invalid(HttpContext,
                new Dictionary<string, string[]> { ["eventTypeCode"] = ["The eventTypeCode is required."] });

        var curve = await _riskModelService.GetIDFCurvesForEventTypeAsync(eventTypeCode, returnPeriodYears,
            cancellationToken);
        return curve is null
            ? NotFoundProblem(HttpContext, "IDF_CURVE_NOT_FOUND", "IDF curve not found.")
            : Ok(Envelope(StatusCodes.Status200OK, curve));
    }

    [HttpGet("regional-risk-parameters/{regionId:guid}")]
    [Authorize(Policy = RiskCatalogPolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<RegionalRiskParametersDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetRegionalParameters(Guid regionId, CancellationToken cancellationToken)
    {
        var parameters = await _riskModelService.GetRegionalRiskParametersAsync(regionId, cancellationToken);
        return parameters is null
            ? NotFoundProblem(HttpContext, "REGIONAL_PARAMETERS_NOT_FOUND", "Regional risk parameters not found.")
            : Ok(Envelope(StatusCodes.Status200OK, parameters));
    }

    [HttpPost("risk-matrices")]
    [Authorize(Policy = RiskCatalogPolicies.Write)]
    [ProducesResponseType<ResponseBaseDTO<CreatedResourceDTO>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> CreateRiskMatrix([FromBody] RiskMatrixDTO request,
        CancellationToken cancellationToken)
    {
        return await _riskModelService.CreateRiskMatrixAsync(request, cancellationToken)
            ? StatusCode(StatusCodes.Status201Created,
                Envelope(StatusCodes.Status201Created, new CreatedResourceDTO(request.EventTypeCode)))
            : PersistenceFailed(HttpContext);
    }

    [HttpPost("idf-curves")]
    [Authorize(Policy = RiskCatalogPolicies.Write)]
    [ProducesResponseType<ResponseBaseDTO<CreatedResourceDTO>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> CreateIdfCurve([FromBody] CreateIDFCurvesDTO request,
        CancellationToken cancellationToken)
    {
        return await _riskModelService.CreateIDFCurvesAsync(request, cancellationToken)
            ? StatusCode(StatusCodes.Status201Created,
                Envelope(StatusCodes.Status201Created, new CreatedResourceDTO(request.EventTypeCode)))
            : PersistenceFailed(HttpContext);
    }

    [HttpPost("regional-risk-parameters")]
    [Authorize(Policy = RiskCatalogPolicies.Write)]
    [ProducesResponseType<ResponseBaseDTO<CreatedResourceDTO>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> CreateRegionalParameters([FromBody] CreateRegionalRiskParameterDTO request,
        CancellationToken cancellationToken)
    {
        return await _riskModelService.CreateRegionalRiskParametersAsync(request, cancellationToken)
            ? StatusCode(StatusCodes.Status201Created,
                Envelope(StatusCodes.Status201Created, new CreatedResourceDTO(request.Description)))
            : PersistenceFailed(HttpContext);
    }

    /// <summary>Activates a catalog version and publishes risk-catalog-published.</summary>
    [HttpPost("catalog-publications")]
    [Authorize(Policy = RiskCatalogPolicies.Write)]
    [ProducesResponseType<ResponseBaseDTO<CatalogPublicationDTO>>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Publish([FromBody] CatalogPublishDTO request, CancellationToken cancellationToken)
    {
        if (!PublisherTenantResolver.TryGetTenantId(User, out var tenantId))
            return new ProblemResult(ApiProblems.Create(HttpContext, StatusCodes.Status403Forbidden,
                "TENANT_CONTEXT_REQUIRED", "The authenticated identity has no tenant to publish on behalf of."));

        return await _riskModelService.PublishCatalogVersionAsync(request, tenantId, cancellationToken)
            ? Accepted(Envelope(StatusCodes.Status202Accepted, new CatalogPublicationDTO(request.Version)))
            : PersistenceFailed(HttpContext);
    }
}
