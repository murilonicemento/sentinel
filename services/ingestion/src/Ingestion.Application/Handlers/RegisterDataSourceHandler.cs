using Ingestion.Application.Commands;
using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.ValueObjects;
using MediatR;

namespace Ingestion.Application.Handlers;

public class RegisterDataSourceHandler : IRequestHandler<RegisterDataSourceCommand, (Guid dataSourceId, Guid tenantId)>
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IDataSourceRepository _dataSourceRepository;

    public RegisterDataSourceHandler(ITenantRepository tenantRepository, IDataSourceRepository dataSourceRepository)
    {
        _tenantRepository = tenantRepository;
        _dataSourceRepository = dataSourceRepository;
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

        var dataSource = new DataSource(
            Guid.NewGuid(),
            request.Name,
            request.Endpoint,
            DataSourceType.From(request.DataSourceType).Value,
            MeasurementType.From(request.MeasurementType).Value,
            CollectionFrequencyType.From(request.CollectionFrequency).Value,
            request.TenantId
        );

        return await _dataSourceRepository.RegisterAsync(dataSource);
    }
}