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
        var createdEventType = await _eventTypeService.CreateEventTypeAsync(eventTypeDto);

        return Created("/api/event-types", new { eventTypeCode = createdEventType.Code });
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "RiskCatalogWrite")]
    public async Task<ActionResult> UpdateEventTypeStatus(
        [FromRoute] Guid id,
        [FromQuery] bool isActive)
    {
        return NoContent();
    }

    [HttpPost("severity-criterion")]
    [Authorize(Policy = "RiskCatalogWrite")]
    public async Task<ActionResult> AddSeverityCriterionToEventType(
        [FromBody] SeverityCriterionDTO severityCriterionDto)
    {
        return NoContent();
    }
}