using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiskCatalog.Api.Contracts;
using RiskCatalog.Api.Security;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Services;
using static RiskCatalog.Api.Controllers.V1.CatalogV1Responses;

namespace RiskCatalog.Api.Controllers.V1;

/// <summary>
/// Paged listings of catalog data, ordered deterministically so a consumer can copy a whole catalog version page by
/// page. Matrix entries and severity criteria filter by <c>version</c> when given; regional parameters have no version.
/// </summary>
[ApiController]
[Route(BasePath)]
public class CatalogListingV1Controller : ControllerBase
{
    private readonly CatalogListingService _listing;

    public CatalogListingV1Controller(CatalogListingService listing)
    {
        _listing = listing;
    }

    [HttpGet("regional-risk-parameters")]
    [Authorize(Policy = RiskCatalogPolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<PagedResultDTO<RegionalRiskParametersDTO>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> ListRegionalParameters([FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryResolve(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var errors))
            return Invalid(HttpContext, errors);

        return Ok(Envelope(StatusCodes.Status200OK,
            await _listing.GetRegionalParametersAsync(resolvedPage, resolvedPageSize, cancellationToken)));
    }

    [HttpGet("risk-matrix-entries")]
    [Authorize(Policy = RiskCatalogPolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<PagedResultDTO<RiskMatrixDTO>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> ListRiskMatrixEntries([FromQuery] int? version, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        if (!Pagination.TryResolve(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var errors))
            return Invalid(HttpContext, errors);

        return Ok(Envelope(StatusCodes.Status200OK,
            await _listing.GetRiskMatrixEntriesAsync(version, resolvedPage, resolvedPageSize, cancellationToken)));
    }

    [HttpGet("severity-criteria")]
    [Authorize(Policy = RiskCatalogPolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<PagedResultDTO<SeverityCriterionDTO>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> ListSeverityCriteria([FromQuery] int? version, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        if (!Pagination.TryResolve(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var errors))
            return Invalid(HttpContext, errors);

        return Ok(Envelope(StatusCodes.Status200OK,
            await _listing.GetSeverityCriteriaAsync(version, resolvedPage, resolvedPageSize, cancellationToken)));
    }
}
