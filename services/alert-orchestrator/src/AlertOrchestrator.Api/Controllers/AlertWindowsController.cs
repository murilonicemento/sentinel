using AlertOrchestrator.Application.DTOs;
using AlertOrchestrator.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AlertOrchestrator.Api.Controllers;

[ApiController]
[Route("api/alert-windows")]
public class AlertWindowsController : ControllerBase
{
    private readonly IAlertWindowQueryService _queryService;

    public AlertWindowsController(IAlertWindowQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AlertWindowDTO>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var window = await _queryService.GetByIdAsync(id, cancellationToken);

        if (window is null) return NoContent();

        return Ok(window);
    }

    [HttpGet("by-region/{region}")]
    public async Task<ActionResult<IReadOnlyList<AlertWindowDTO>>> GetByRegion(
        string region,
        CancellationToken cancellationToken)
    {
        var windows = await _queryService.GetByRegionAsync(region, cancellationToken);
        return Ok(windows);
    }

    [HttpGet("by-status/{status}")]
    public async Task<ActionResult<IReadOnlyList<AlertWindowDTO>>> GetByStatus(
        string status,
        CancellationToken cancellationToken)
    {
        var windows = await _queryService.GetByStatusAsync(status, cancellationToken);
        return Ok(windows);
    }
}