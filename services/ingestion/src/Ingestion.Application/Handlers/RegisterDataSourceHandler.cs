using Ingestion.Application.Commands;
using Ingestion.Application.Errors;
using Ingestion.Application.Interfaces.Repositories;
using Ingestion.Application.Interfaces.Services;
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
    private readonly ITenantBillingGateway _tenantBillingGateway;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterDataSourceHandler(ITenantRepository tenantRepository, IDataSourceRepository dataSourceRepository,
        IEventTypePermissionRepository eventTypePermissionRepository, ITenantBillingGateway tenantBillingGateway,
        IUnitOfWork unitOfWork)
    {
        _tenantRepository = tenantRepository;
        _dataSourceRepository = dataSourceRepository;
        _eventTypePermissionRepository = eventTypePermissionRepository;
        _tenantBillingGateway = tenantBillingGateway;
        _unitOfWork = unitOfWork;
    }

    public async Task<(Guid dataSourceId, Guid tenantId)> Handle(
        RegisterDataSourceCommand request,
        CancellationToken cancellationToken)
    {
        var tenantExists = await _tenantRepository.ExistsAsync(request.TenantId);

        if (!tenantExists)
            throw new IngestionReferenceNotFoundException(IngestionErrorCodes.TenantNotFound,
                $"Tenant {request.TenantId} not found.");

        var isTenantActive = await _tenantBillingGateway.ValidateTenantAsync(request.TenantId, cancellationToken);
        if (!isTenantActive)
            throw new IngestionRuleViolationException(IngestionErrorKind.Unprocessable, IngestionErrorCodes.TenantInactive,
                $"Tenant {request.TenantId} is not active or is not authorized in Tenants Billing.");

        var existing = await _dataSourceRepository.GetByNameAndTenantAsync(request.Name, request.TenantId);

        if (existing != null)
            throw new IngestionRuleViolationException(IngestionErrorKind.Conflict, IngestionErrorCodes.DataSourceAlreadyExists,
                $"DataSource '{request.Name}' already exists for this tenant.");

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

        // The data source and its event permissions are written together, never one without the other.
        (Guid dataSourceId, Guid tenantId) registered = default;
        await _unitOfWork.ExecuteAsync(async _ =>
        {
            registered = await _dataSourceRepository.RegisterAsync(dataSource);
            if (!await _eventTypePermissionRepository.RegisterManyAsync(eventPermissions))
                throw new Exception("Unexpected exception occurred.");
        }, cancellationToken);

        return registered;
    }
}