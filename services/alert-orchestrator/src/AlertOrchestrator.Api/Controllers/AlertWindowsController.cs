using AlertOrchestrator.Api.Contracts;
using AlertOrchestrator.Api.Security;
using AlertOrchestrator.Application.DTOs;
using AlertOrchestrator.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlertOrchestrator.Api.Controllers;

[ApiController]
[Route("api/alert-windows")]
[Authorize(Policy = AlertOrchestratorPolicies.Read)]
[DeprecatedRoute("/" + AlertWindowsV1Controller.BasePath)]
public class AlertWindowsController : ControllerBase
{
    private readonly IAlertWindowQueryService _queryService;
    private readonly TenantAccessEvaluator _tenantAccess;

    public AlertWindowsController(IAlertWindowQueryService queryService, TenantAccessEvaluator tenantAccess)
    {
        _queryService = queryService;
        _tenantAccess = tenantAccess;
    }

    private AlertWindowAccessScope AccessScope() =>
        _tenantAccess.IsServicePrincipal(User)
            ? AlertWindowAccessScope.Platform
            : AlertWindowAccessScope.ForTenants(User.FindAll(SentinelClaims.TenantId).Select(c => c.Value));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AlertWindowDTO>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var window = await _queryService.GetByIdAsync(id, AccessScope(), cancellationToken);

        if (window is null) return NoContent();

        return Ok(window);
    }

    [HttpGet("by-region/{region}")]
    public async Task<ActionResult<IReadOnlyList<AlertWindowDTO>>> GetByRegion(
        string region,
        CancellationToken cancellationToken)
    {
        var windows = await _queryService.GetByRegionAsync(region, AccessScope(), cancellationToken);
        return Ok(windows);
    }

    [HttpGet("by-status/{status}")]
    public async Task<ActionResult<IReadOnlyList<AlertWindowDTO>>> GetByStatus(
        string status,
        CancellationToken cancellationToken)
    {
        var windows = await _queryService.GetByStatusAsync(status, AccessScope(), cancellationToken);
        return Ok(windows);
    }
}