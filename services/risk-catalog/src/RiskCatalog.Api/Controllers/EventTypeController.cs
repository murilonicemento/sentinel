using MediatR;
using Microsoft.AspNetCore.Mvc;
using RiskCatalog.Application.DTO;

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
    public ActionResult<EventTypeDTO> GetEventTypes([FromQuery] bool? isActive)
    {
        // Implementation to retrieve event types would go here.
        return Ok();
    }

    [HttpGet("event-type-by-code")]
    public ActionResult<EventTypeDTO> GetEventTypeByCode([FromQuery] string eventTypeCode)
    {
        // Implementation to retrieve event types would go here.
        return Ok();
    }

    [HttpGet("severity-criterion")]
    public ActionResult<SeverityCriterionDTO> GetSeverityCriterionForEventType(
        [FromQuery] string eventTypeCode,
        [FromQuery] int? version
    )
    {
        // Implementation to retrieve event types would go here.
        return Ok();
    }
}