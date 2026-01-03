using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Services.Interfaces;

namespace RiskCatalog.Api.Controllers;

[Route("api/event-types")]
[ApiController]
public class EventTypeController : ControllerBase
{
    private readonly IEventTypeService _eventTypeService;

    public EventTypeController(IEventTypeService eventTypeService)
    {
        _eventTypeService = eventTypeService;
    }

    [HttpGet]
    public async Task<ActionResult<List<EventTypeDTO>>> GetEventTypes([FromQuery] bool? isActive)
    {
        var eventTypes = await _eventTypeService.GetEventTypesAsync(isActive);

        return eventTypes.Count != 0 ? Ok(eventTypes) : NoContent();
    }

    [HttpGet("event-type-by-code")]
    public async Task<ActionResult<EventTypeDTO>> GetEventTypeByCode([FromQuery] string eventTypeCode)
    {
        var eventType = await _eventTypeService.GetEventTypeByCodeAsync(eventTypeCode);

        return eventType is not null ? Ok(eventType) : NoContent();
    }

    [HttpGet("severity-criterion")]
    public async Task<ActionResult<List<SeverityCriterionDTO>>> GetSeverityCriterionForEventType(
        [FromQuery] string eventTypeCode,
        [FromQuery] int? version
    )
    {
        var severityCriterion = await _eventTypeService.GetSeverityCriterionForEventTypeAsync(
            eventTypeCode,
            version);

        return severityCriterion.Count != 0 ? Ok(severityCriterion) : NoContent();
    }

    [HttpPost]
    [Authorize(Policy = "RiskCatalogWrite")]
    public async Task<ActionResult<EventTypeDTO>> CreateEventType([FromBody] CreateEventTypeDTO eventTypeDto)
    {
        var isCreated = await _eventTypeService.CreateEventTypeAsync(eventTypeDto);

        return Created("/api/event-types", new { isCreated });
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "RiskCatalogWrite")]
    public async Task<ActionResult> UpdateEventTypeStatus(
        [FromRoute] Guid id,
        [FromQuery] bool isActive)
    {
        var isUpdated = await _eventTypeService.UpdateEventTypeStatusAsync(id, isActive);

        return isUpdated
            ? Ok()
            : Problem(
                title: "An error occurred.",
                detail: "Failed to update event type status.",
                type: "UpdateError",
                statusCode: 500
            );
    }

    [HttpPost("severity-criterion")]
    [Authorize(Policy = "RiskCatalogWrite")]
    public async Task<ActionResult> AddSeverityCriterionToEventType(
        [FromBody] SeverityCriterionDTO severityCriterionDto)
    {
        var isAdded = await _eventTypeService.AddSeverityCriterionToEventType(severityCriterionDto);

        return isAdded
            ? Created("/api/event-types/severity-criterion", new { isAdded })
            : Problem(
                title: "An error occurred.",
                detail: "Failed to add severity criterion to event type.",
                type: "AddError",
                statusCode: 500
            );
    }
}