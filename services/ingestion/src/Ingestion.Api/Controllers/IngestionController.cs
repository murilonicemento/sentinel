using Ingestion.Api.Contracts;
using Ingestion.Api.Security;
using Ingestion.Application.Commands;
using Ingestion.Application.DTO;
using Ingestion.Application.Events;
using Ingestion.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ingestion.Api.Controllers;

[Route("api/ingestion")]
[DeprecatedRoute("/" + IngestionV1Controller.BasePath)]
[ApiController]
[Authorize]
public class IngestionController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly TenantAccessEvaluator _tenantAccess;

    public IngestionController(IMediator mediator, TenantAccessEvaluator tenantAccess)
    {
        _mediator = mediator;
        _tenantAccess = tenantAccess;
    }

    [HttpGet("last-detected-events")]
    [Authorize(Policy = "IngestionRead")]
    public async Task<ActionResult<ResponseBaseDTO<IEnumerable<SensorEventDetected>>>> GetLastDetectedEvents(
        [FromQuery] int limit = 50)
    {
        var query = new GetLatestDetectedEventsQuery { Limit = limit };
        var lastDetectedEvents = await _mediator.Send(query);

        return Ok(lastDetectedEvents);
    }

    [HttpGet("collection-statistics")]
    [Authorize(Policy = "IngestionRead")]
    public async Task<ActionResult<ResponseBaseDTO<CollectionStatisticsResponseDTO>>> GetCollectionStatistics(
        [FromQuery] DateTime? initialDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var query = new GetCollectionStatisticsQuery { InitialDate = initialDate, EndDate = endDate };
        var lastDetectedEvents = await _mediator.Send(query);

        return Ok(lastDetectedEvents);
    }

    [HttpPost("data-source")]
    [Authorize(Policy = "IngestionWrite")]
    public async Task<ActionResult<ResponseBaseDTO<RegisterDatasourceResponseDTO>>> RegisterDatasource(
        [FromBody] RegisterDataSourceCommand command)
    {
        if (!_tenantAccess.CanAccessTenant(User, command.TenantId)) return Forbid();

        var (dataSourceId, tenantId) = await _mediator.Send(command);

        return Created(
            "api/ingestion/data-source",
            new RegisterDatasourceResponseDTO { DataSourceId = dataSourceId, TenantId = tenantId }
        );
    }

    [HttpPost("climatic")]
    [Authorize(Policy = "IngestionWrite")]
    public async Task<ActionResult<ResponseBaseDTO<Guid>>> RegisterClimaticEvent(
        [FromBody] RegisterClimaticEventCommand command)
    {
        if (!_tenantAccess.CanAccessTenant(User, command.TenantId)) return Forbid();

        var climaticEventId = await _mediator.Send(command);

        return Created("api/ingestion/climatic", new { ClimaticEventId = climaticEventId });
    }

    [HttpPost("disaster")]
    [Authorize(Policy = "IngestionWrite")]
    public async Task<ActionResult<ResponseBaseDTO<Guid>>> RegisterDisasterEvent(
        [FromBody] RegisterDisasterEventCommand command)
    {
        if (!_tenantAccess.CanAccessTenant(User, command.TenantId)) return Forbid();

        var disasterEventId = await _mediator.Send(command);

        return Created("api/ingestion/disaster", new { DisasterEventId = disasterEventId });
    }

    [HttpPost("sensor-collection")]
    [Authorize(Policy = "IngestionWrite")]
    public async Task<ActionResult<ResponseBaseDTO<Guid>>> RegisterSensorCollection(
        [FromBody] RegisterSensorCollectionCommand command)
    {
        if (!_tenantAccess.CanAccessTenant(User, command.TenantId)) return Forbid();

        var sensorCollectionId = await _mediator.Send(command);

        return Created("api/ingestion/sensor-collection", new { SensorCollectionId = sensorCollectionId });
    }
}