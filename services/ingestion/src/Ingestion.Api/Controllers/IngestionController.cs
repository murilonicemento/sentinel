using Ingestion.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Ingestion.Api.Controllers;

[Route("api/ingestion")]
[ApiController]
public class IngestionController : ControllerBase
{
    private readonly IMediator _mediator;

    public IngestionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("data-source")]
    public async Task<ActionResult<Guid>> RegisterDatasource(RegisterDataSourceCommand command)
    {
        var dataSourceId = await _mediator.Send(command);
        return Created("api/ingestion/data-source", new { id = dataSourceId });
    }

    [HttpPost("sensor-collection")]
    public ActionResult<Guid> RegisterSensorCollection([FromBody] RegisterSensorCollectionCommand command)
    {
        return Created();
    }
}