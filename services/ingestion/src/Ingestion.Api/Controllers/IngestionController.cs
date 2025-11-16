using Ingestion.Application.Commands;
using Ingestion.Application.DTO;
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
    public async Task<ActionResult<RegisterDatasourceResponseDTO>> RegisterDatasource(RegisterDataSourceCommand command)
    {
        var (dataSourceId, tenantId) = await _mediator.Send(command);

        return Created(
            "api/ingestion/data-source",
            new RegisterDatasourceResponseDTO { DataSourceId = dataSourceId, TenantId = tenantId }
        );
    }

    [HttpPost("sensor-collection")]
    public async Task<ActionResult<Guid>> RegisterSensorCollection([FromBody] RegisterSensorCollectionCommand command)
    {
        var sensorCollectionId = await _mediator.Send(command);

        return Created("api/ingestion/sensor-collection", new { sensorCollectionId });
    }
}