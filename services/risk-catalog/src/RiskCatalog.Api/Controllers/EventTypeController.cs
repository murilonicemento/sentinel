using Microsoft.AspNetCore.Mvc;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Services.Interfaces;

namespace RiskCatalog.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EventTypeController : ControllerBase
{
    private readonly IEventTypeService _eventTypeService;

    public EventTypeController(IEventTypeService eventTypeService)
    {
        _eventTypeService = eventTypeService;
    }

    [HttpGet("event-types")]
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
}