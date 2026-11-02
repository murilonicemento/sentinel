using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiskCatalog.Api.Contracts;
using RiskCatalog.Api.Security;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Services.Interfaces;
using static RiskCatalog.Api.Controllers.V1.CatalogV1Responses;

namespace RiskCatalog.Api.Controllers.V1;

public sealed record UpdateEventTypeStatusRequest(bool IsActive);

[ApiController]
[Route(BasePath)]
public class EventTypesV1Controller : ControllerBase
{
    private readonly IEventTypeService _eventTypeService;

    public EventTypesV1Controller(IEventTypeService eventTypeService)
    {
        _eventTypeService = eventTypeService;
    }

    /// <summary>Lists event types ordered by code (ordinal) and id.</summary>
    [HttpGet("event-types")]
    [Authorize(Policy = RiskCatalogPolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<PagedResultDTO<EventTypeDTO>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> List([FromQuery] bool? isActive, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!Pagination.TryResolve(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var errors))
            return Invalid(HttpContext, errors);

        var eventTypes = (await _eventTypeService.GetEventTypesAsync(isActive, cancellationToken))
            .OrderBy(e => e.Code, StringComparer.Ordinal)
            .ThenBy(e => e.Id)
            .ToList();
        return Ok(Envelope(StatusCodes.Status200OK, PagedResultDTO<EventTypeDTO>.From(eventTypes, resolvedPage,
            resolvedPageSize)));
    }

    [HttpGet("event-types/{code}")]
    [Authorize(Policy = RiskCatalogPolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<EventTypeDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetByCode(string code, CancellationToken cancellationToken)
    {
        var eventType = await _eventTypeService.GetEventTypeByCodeAsync(code, cancellationToken);
        return eventType is null
            ? NotFoundProblem(HttpContext, "EVENT_TYPE_NOT_FOUND", "Event type not found.")
            : Ok(Envelope(StatusCodes.Status200OK, eventType));
    }

    /// <summary>Severity criteria of an event type ordered by version, severity level and minimum value.</summary>
    [HttpGet("event-types/{code}/severity-criteria")]
    [Authorize(Policy = RiskCatalogPolicies.Read)]
    [ProducesResponseType<ResponseBaseDTO<PagedResultDTO<SeverityCriterionDTO>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> ListSeverityCriteria(string code, [FromQuery] int? version, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        if (!Pagination.TryResolve(page, pageSize, out var resolvedPage, out var resolvedPageSize, out var errors))
            return Invalid(HttpContext, errors);

        var criteria = (await _eventTypeService.GetSeverityCriterionForEventTypeAsync(code, version, cancellationToken))
            .OrderBy(c => c.Version)
            .ThenBy(c => c.SeverityLevel, StringComparer.Ordinal)
            .ThenBy(c => c.MinValue)
            .ToList();
        return Ok(Envelope(StatusCodes.Status200OK, PagedResultDTO<SeverityCriterionDTO>.From(criteria, resolvedPage,
            resolvedPageSize)));
    }

    [HttpPost("event-types")]
    [Authorize(Policy = RiskCatalogPolicies.Write)]
    [ProducesResponseType<ResponseBaseDTO<CreatedResourceDTO>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Create([FromBody] CreateEventTypeDTO request, CancellationToken cancellationToken)
    {
        return await _eventTypeService.CreateEventTypeAsync(request, cancellationToken)
            ? Created($"/{BasePath}/event-types/{request.Code}",
                Envelope(StatusCodes.Status201Created, new CreatedResourceDTO(request.Code)))
            : PersistenceFailed(HttpContext);
    }

    [HttpPatch("event-types/{id:guid}/status")]
    [Authorize(Policy = RiskCatalogPolicies.Write)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateEventTypeStatusRequest request,
        CancellationToken cancellationToken)
    {
        return await _eventTypeService.UpdateEventTypeStatusAsync(id, request.IsActive, cancellationToken)
            ? NoContent()
            : NotFoundProblem(HttpContext, "EVENT_TYPE_NOT_FOUND", "Event type not found.");
    }

    [HttpPost("severities")]
    [Authorize(Policy = RiskCatalogPolicies.Write)]
    [ProducesResponseType<ResponseBaseDTO<CreatedResourceDTO>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> CreateSeverity([FromBody] SeverityDTO request, CancellationToken cancellationToken)
    {
        return await _eventTypeService.CreateSeverity(request, cancellationToken)
            ? StatusCode(StatusCodes.Status201Created,
                Envelope(StatusCodes.Status201Created, new CreatedResourceDTO(request.Level)))
            : PersistenceFailed(HttpContext);
    }

    [HttpPost("severity-criteria")]
    [Authorize(Policy = RiskCatalogPolicies.Write)]
    [ProducesResponseType<ResponseBaseDTO<CreatedResourceDTO>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> CreateSeverityCriterion([FromBody] SeverityCriterionDTO request,
        CancellationToken cancellationToken)
    {
        return await _eventTypeService.CreateSeverityCriterionToEventType(request, cancellationToken)
            ? Created($"/{BasePath}/event-types/{request.EventTypeCode}/severity-criteria",
                Envelope(StatusCodes.Status201Created, new CreatedResourceDTO(request.EventTypeCode)))
            : PersistenceFailed(HttpContext);
    }
}
