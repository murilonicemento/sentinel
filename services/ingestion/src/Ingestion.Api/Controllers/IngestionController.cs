using Ingestion.Application.Commands;
using Ingestion.Application.DTO;
using Ingestion.Application.Events;
using Ingestion.Application.Queries;
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

    [HttpGet("last-detected-events")]
    public async Task<ActionResult<ResponseBaseDTO<IEnumerable<ClimaticEventDetectedEvent>>>> GetLastDetectedEvents(
        [FromQuery] int limit = 50)
    {
        var query = new GetLatestDetectedEventsQuery { Limit = limit };
        var lastDetectedEvents = await _mediator.Send(query);

        return Ok(lastDetectedEvents);
    }

    [HttpGet("collection-statistics")]
    public async Task<ActionResult<ResponseBaseDTO<CollectionStatisticsResponseDTO>>> GetCollectionStatistics(
        [FromQuery] DateTime? initialDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var query = new GetCollectionStatisticsQuery { InitialDate = initialDate, EndDate = endDate };
        var lastDetectedEvents = await _mediator.Send(query);

        return Ok(lastDetectedEvents);
    }

    [HttpPost("data-source")]
    public async Task<ActionResult<ResponseBaseDTO<RegisterDatasourceResponseDTO>>> RegisterDatasource(
        [FromBody] RegisterDataSourceCommand command)
    {
        var (dataSourceId, tenantId) = await _mediator.Send(command);

        return Created(
            "api/ingestion/data-source",
            new RegisterDatasourceResponseDTO { DataSourceId = dataSourceId, TenantId = tenantId }
        );
    }

    [HttpPost("sensor-collection")]
    public async Task<ActionResult<ResponseBaseDTO<Guid>>> RegisterSensorCollection(
        [FromBody] RegisterSensorCollectionCommand command)
    {
        var sensorCollectionId = await _mediator.Send(command);

        return Created("api/ingestion/sensor-collection", new { sensorCollectionId });
    }
}