using AlertOrchestrator.Api.Contracts;
using AlertOrchestrator.Api.Security;
using AlertOrchestrator.Application.DTOs;
using AlertOrchestrator.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlertOrchestrator.Api.Controllers;

[ApiController]
[Route(BasePath)]
[Authorize(Policy = AlertOrchestratorPolicies.Read)]
public class AlertWindowsV1Controller : ControllerBase
{
    public const string BasePath = "api/v1/alert-orchestrator/alert-windows";

    private readonly IAlertWindowQueryService _queryService;
    private readonly TenantAccessEvaluator _tenantAccess;

    public AlertWindowsV1Controller(IAlertWindowQueryService queryService, TenantAccessEvaluator tenantAccess)
    {
        _queryService = queryService;
        _tenantAccess = tenantAccess;
    }

    /// <summary>Returns an alert window of one of the caller's tenants.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ResponseBaseDTO<AlertWindowDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var window = await _queryService.GetByIdAsync(id, AccessScope(), cancellationToken);

        return window is null
            ? new ProblemResult(ApiProblems.Create(HttpContext, StatusCodes.Status404NotFound, "ALERT_WINDOW_NOT_FOUND",
                "Alert window not found."))
            : Ok(Envelope(window));
    }

    /// <summary>
    /// Lists alert windows filtered by region and/or status (at least one is required), newest first and then by
    /// id. Only windows of the caller's tenants are returned.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ResponseBaseDTO<PagedResultDTO<AlertWindowDTO>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Search(
        [FromQuery] string? region,
        [FromQuery] string? status,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryResolve(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var errors))
            return new ProblemResult(ApiProblems.Create(HttpContext, StatusCodes.Status400BadRequest,
                ApiProblems.ValidationError, "One or more fields are invalid.", errors));

        var result = await _queryService.SearchAsync(region, status, AccessScope(), resolvedPage, resolvedPageSize,
            cancellationToken);
        return Ok(Envelope(result));
    }

    private AlertWindowAccessScope AccessScope() =>
        _tenantAccess.IsServicePrincipal(User)
            ? AlertWindowAccessScope.Platform
            : AlertWindowAccessScope.ForTenants(_tenantAccess.TenantIds(User));

    private static ResponseBaseDTO<T> Envelope<T>(T data) =>
        new() { StatusCode = StatusCodes.Status200OK, Success = true, Data = data };
}
