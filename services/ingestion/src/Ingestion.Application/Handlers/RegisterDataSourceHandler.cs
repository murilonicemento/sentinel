using Ingestion.Application.Commands;
using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.ValueObjects;
using MediatR;

namespace Ingestion.Application.Handlers;

public class RegisterDataSourceHandler : IRequestHandler<RegisterDataSourceCommand, (Guid dataSourceId, Guid tenantId)>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IDataSourceRepository _dataSourceRepository;
    private readonly IEventTypePermissionRepository _eventTypePermissionRepository;

    public RegisterDataSourceHandler(ITenantRepository tenantRepository, IDataSourceRepository dataSourceRepository,
        IEventTypePermissionRepository eventTypePermissionRepository)
    {
        _tenantRepository = tenantRepository;
        _dataSourceRepository = dataSourceRepository;
        _eventTypePermissionRepository = eventTypePermissionRepository;
    }

    public async Task<(Guid dataSourceId, Guid tenantId)> Handle(
        RegisterDataSourceCommand request,
        CancellationToken cancellationToken)
    {
        var tenantExists = await _tenantRepository.ExistsAsync(request.TenantId);

        if (!tenantExists)
            throw new KeyNotFoundException($"Tenant {request.TenantId} not found.");

        var existing = await _dataSourceRepository.GetByNameAndTenantAsync(request.Name, request.TenantId);

        if (existing != null)
            throw new InvalidOperationException($"DataSource '{request.Name}' already exists for this tenant.");

        var dataSourceId = Guid.NewGuid();
        var dataSource = new DataSource(
            dataSourceId,
            request.Name,
            request.Endpoint,
            DataSourceType.From(request.DataSourceType).Value,
            MeasurementType.From(request.MeasurementType).Value,
            CollectionFrequencyType.From(request.CollectionFrequency).Value,
            request.TenantId
        );
        var eventPermissions = request.EventPermissions
            .Select(eventTypePermission => new EventTypePermission
            {
                Id = Guid.NewGuid(),
                DataSourceId = dataSourceId,
                EventDomain = eventTypePermission.EventDomain,
                EventType = eventTypePermission.EventType
            }).ToList();

        var (_, tenantId) = await _dataSourceRepository.RegisterAsync(dataSource);

        if (!await _eventTypePermissionRepository.RegisterManyAsync(eventPermissions))
            throw new Exception("Unexpected exception occurred.");

        return (dataSourceId, tenantId);
    }
}