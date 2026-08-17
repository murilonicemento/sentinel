using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Enums;
using TenantsBilling.Domain.Events;
using TenantsBilling.Domain.Repositories;

namespace TenantsBilling.Application.Services;

public sealed record CreateTenantCommand(string Name, Guid PlanId, string? Region = null, string? TimeZone = null);

public sealed class TenantManagementService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IPlanRepository _planRepository;
    private readonly ITenantEventPublisher _eventPublisher;

    public TenantManagementService(ITenantRepository tenantRepository, IPlanRepository planRepository, ITenantEventPublisher eventPublisher)
    {
        _tenantRepository = tenantRepository;
        _planRepository = planRepository;
        _eventPublisher = eventPublisher;
    }

    public async Task<Tenant> CreateTenantAsync(CreateTenantCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Tenant name is required.", nameof(command));

        if (command.PlanId == Guid.Empty)
            throw new ArgumentException("Plan id is required.", nameof(command));

        var existingTenant = await _tenantRepository.GetByNameAsync(command.Name, cancellationToken);
        if (existingTenant is not null)
            throw new InvalidOperationException($"Tenant '{command.Name}' already exists.");

        var plan = await _planRepository.GetByIdAsync(command.PlanId, cancellationToken);
        if (plan is null)
            throw new InvalidOperationException("Plan not found.");

        var tenant = Tenant.Create(command.Name, command.PlanId, command.Region, command.TimeZone);
        await _tenantRepository.AddAsync(tenant, cancellationToken);
        await _eventPublisher.PublishAsync("TenantCreated", new { tenant.Id, tenant.Name, tenant.PlanId, tenant.Region, tenant.TimeZone }, cancellationToken);

        return tenant;
    }

    public async Task<TenantQuotaStatus> RegisterUsageAsync(Guid tenantId, int eventsConsumed, int alertsTriggered, int apiRequests, int channelUsage, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant not found.");

        var status = tenant.RecordUsage(eventsConsumed, alertsTriggered, apiRequests, channelUsage);
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);

        if (status == TenantQuotaStatus.SoftLimit)
        {
            await _eventPublisher.PublishAsync("TenantQuotaSoftLimitReached", new { tenant.Id, eventsConsumed, alertsTriggered, apiRequests, channelUsage }, cancellationToken);
        }
        else if (status == TenantQuotaStatus.HardLimit)
        {
            await _eventPublisher.PublishAsync("TenantQuotaHardLimitReached", new { tenant.Id, eventsConsumed, alertsTriggered, apiRequests, channelUsage }, cancellationToken);
        }

        return status;
    }

    public async Task<Tenant> SuspendTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Tenant not found.");

        tenant.Suspend();
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        await _eventPublisher.PublishAsync("TenantSuspended", new { tenant.Id, tenant.Name, tenant.Status }, cancellationToken);
        return tenant;
    }
}
