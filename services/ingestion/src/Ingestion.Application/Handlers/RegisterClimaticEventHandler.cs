using Ingestion.Application.Commands;
using Ingestion.Application.Events;
using Ingestion.Application.Interfaces.Services;
using MediatR;

namespace Ingestion.Application.Handlers;

public class RegisterClimaticEventHandler : IRequestHandler<RegisterClimaticEventCommand, Guid>
{
    private readonly ISensorCollectionService _sensorCollectionService;

    public RegisterClimaticEventHandler(ISensorCollectionService sensorCollectionService)
    {
        _sensorCollectionService = sensorCollectionService;
    }

    public async Task<Guid> Handle(RegisterClimaticEventCommand request, CancellationToken cancellationToken)
        => await _sensorCollectionService.ProcessSensorCollection<SensorEventDetected>(
            request.DatasourceId, request.TenantId, request.CollectedAt,
            request.Payload, request.SampleSensors, "Climatic", cancellationToken);
}