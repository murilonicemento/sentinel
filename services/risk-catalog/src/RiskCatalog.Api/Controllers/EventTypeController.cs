using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Queries;

namespace RiskCatalog.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EventTypeController : ControllerBase
{
    private readonly IMediator _mediator;

    public EventTypeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("event-types")]
    public async Task<ActionResult<List<EventTypeDTO>>> GetEventTypes([FromQuery] bool? isActive)
    {
        var query = new GetEventTypesQuery { IsActive = isActive };
        var eventTypes = await _mediator.Send(query);

        return eventTypes.Count != 0 ? Ok(eventTypes) : NoContent();
    }

    [HttpGet("event-type-by-code")]
    public async Task<ActionResult<EventTypeDTO>> GetEventTypeByCode([FromQuery] string eventTypeCode)
    {
        var query = new GetEventTypeByCodeQuery { EventTypeCode = eventTypeCode };
        var eventType = await _mediator.Send(query);

        return eventType is not null ? Ok(eventType) : NoContent();
    }

    [HttpGet("severity-criterion")]
    public async Task<ActionResult<List<SeverityCriterionDTO>>> GetSeverityCriterionForEventType(
        [FromQuery] string eventTypeCode,
        [FromQuery] int? version
    )
    {
        var query = new GetSeverityCriterionForEventTypeQuery
        {
            EventTypeCode = eventTypeCode,
            Version = version
        };
        var severityCriterion = await _mediator.Send(query);

        return severityCriterion.Count != 0 ? Ok(severityCriterion) : NoContent();
    }
}