using AlertOrchestrator.Application.DTOs;
using AlertOrchestrator.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace AlertOrchestrator.Api.Controllers;

[ApiController]
[Route("api/alert-windows")]
public class AlertWindowsController : ControllerBase
{
    private readonly IAlertWindowQueryService _queryService;
    private readonly ILogger<AlertWindowsController> _logger;

    public AlertWindowsController(IAlertWindowQueryService queryService, ILogger<AlertWindowsController> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AlertWindowDTO>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var window = await _queryService.GetByIdAsync(id, cancellationToken);
        if (window is null)
        {
            return NotFound();
        }

        return Ok(window);
    }

    [HttpGet("by-region/{region}")]
    public async Task<ActionResult<IReadOnlyList<AlertWindowDTO>>> GetByRegion(string region,
        CancellationToken cancellationToken)
    {
        var windows = await _queryService.GetByRegionAsync(region, cancellationToken);
        return Ok(windows);
    }

    [HttpGet("by-status/{status}")]
    public async Task<ActionResult<IReadOnlyList<AlertWindowDTO>>> GetByStatus(string status,
        CancellationToken cancellationToken)
    {
        var windows = await _queryService.GetByStatusAsync(status, cancellationToken);
        return Ok(windows);
    }
}