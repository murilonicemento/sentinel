using Ingestion.Application.Commands;
using Ingestion.Application.Events;
using Ingestion.Application.Interfaces.Services;
using MediatR;

namespace Ingestion.Application.Handlers;

public class RegisterDisasterEventHandler : IRequestHandler<RegisterDisasterEventCommand, Guid>
{
    private readonly ISensorCollectionService _sensorCollectionService;

    public RegisterDisasterEventHandler(ISensorCollectionService sensorCollectionService)
    {
        _sensorCollectionService = sensorCollectionService;
    }

    public async Task<Guid> Handle(RegisterDisasterEventCommand request, CancellationToken cancellationToken)
        => await _sensorCollectionService.ProcessSensorCollection<SensorEventDetected>(
            request.DatasourceId, request.TenantId, request.CollectedAt,
            request.Payload, request.SampleSensors, "Disaster", cancellationToken);
}