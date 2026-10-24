using Ingestion.Application.Commands;
using Ingestion.Application.Events;
using Ingestion.Application.Interfaces.Services;
using Ingestion.Domain.Interfaces.Repositories;
using MediatR;

namespace Ingestion.Application.Handlers;

public class RegisterSensorCollectionHandler : IRequestHandler<RegisterSensorCollectionCommand, Guid>
{
    private readonly ISensorCollectionService _sensorCollectionService;
    private readonly IDataSourceRepository _dataSourceRepository;

    public RegisterSensorCollectionHandler(
        ISensorCollectionService sensorCollectionService,
        IDataSourceRepository dataSourceRepository)
    {
        _sensorCollectionService = sensorCollectionService;
        _dataSourceRepository = dataSourceRepository;
    }

    public async Task<Guid> Handle(RegisterSensorCollectionCommand request, CancellationToken cancellationToken)
    {
        var dataSource = _dataSourceRepository.GetByIdAndTenantId(request.DatasourceId, request.TenantId)
                         ?? throw new KeyNotFoundException(
                             $"DataSource or tenant not found. DataSource Id: {request.DatasourceId}, TenantId: {request.TenantId}");

        return await _sensorCollectionService.ProcessSensorCollection<SensorEventDetected>(
            request.DatasourceId, request.TenantId, request.CollectedAt,
            request.Payload, request.SampleSensors.ToList(), dataSource.ResolveSensorDomain().ToString(),
            cancellationToken);
    }
}
