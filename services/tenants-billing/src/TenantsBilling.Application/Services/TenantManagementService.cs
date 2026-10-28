using TenantsBilling.Application.Errors;
using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Enums;
using TenantsBilling.Domain.Events;
using TenantsBilling.Domain.Repositories;
using TenantsBilling.Domain.Services;

namespace TenantsBilling.Application.Services;

public sealed record CreateTenantCommand(string Name, Guid PlanId, string? Region = null, string? TimeZone = null);

public sealed class TenantManagementService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IPlanRepository _planRepository;
    private readonly ITenantEventPublisher _eventPublisher;
    private readonly ITenantUsageRepository _usageRepository;
    private readonly TimeProvider _timeProvider;

    public TenantManagementService(ITenantRepository tenantRepository, IPlanRepository planRepository,
        ITenantEventPublisher eventPublisher, ITenantUsageRepository usageRepository, TimeProvider? timeProvider = null)
    {
        _tenantRepository = tenantRepository;
        _planRepository = planRepository;
        _eventPublisher = eventPublisher;
        _usageRepository = usageRepository;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Tenant> CreateTenantAsync(CreateTenantCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Tenant name is required.", nameof(command));

        if (command.PlanId == Guid.Empty)
            throw new ArgumentException("Plan id is required.", nameof(command));

        var existingTenant = await _tenantRepository.GetByNameAsync(command.Name, cancellationToken);
        if (existingTenant is not null)
            throw new TenantsBillingException(TenantsBillingErrorKind.Conflict, TenantsBillingErrorCodes.TenantAlreadyExists,
                $"Tenant '{command.Name}' already exists.");

        var plan = await _planRepository.GetByIdAsync(command.PlanId, cancellationToken);
        if (plan is null)
            throw new TenantsBillingException(TenantsBillingErrorKind.Unprocessable, TenantsBillingErrorCodes.PlanNotFound,
                "Plan not found.");

        var tenant = Tenant.Create(command.Name, command.PlanId, command.Region, command.TimeZone);
        await _tenantRepository.AddAsync(tenant, cancellationToken);
        await _eventPublisher.PublishAsync("TenantCreated", new { tenant.Id, tenant.Name, tenant.PlanId, tenant.Region, tenant.TimeZone }, cancellationToken);

        return tenant;
    }

    public Task<TenantQuotaStatus> RegisterUsageAsync(Guid tenantId, int eventsConsumed, int alertsTriggered, int apiRequests, int channelUsage, CancellationToken cancellationToken = default)
        => RegisterUsageAsync(tenantId, eventsConsumed, alertsTriggered, apiRequests, channelUsage, sourceEventId: null,
            cancellationToken);

    /// <summary>
    /// Adds the usage to the tenant's current billing period (calendar month in the tenant's time zone) and returns the
    /// period's quota status against the plan limits.
    /// </summary>
    /// <param name="sourceEventId">
    /// Identifies the usage event (its EventId, or topic/partition/offset). An event already recorded is not counted
    /// again; the id is also copied to the quota events. Null for direct API calls, which are not deduplicated.
    /// </param>
    public async Task<TenantQuotaStatus> RegisterUsageAsync(Guid tenantId, int eventsConsumed, int alertsTriggered,
        int apiRequests, int channelUsage, string? sourceEventId, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new TenantsBillingException(TenantsBillingErrorKind.NotFound, TenantsBillingErrorCodes.TenantNotFound,
                "Tenant not found.");

        if (tenant.IsBlocked)
            return TenantQuotaStatus.HardLimit;

        var plan = await _planRepository.GetByIdAsync(tenant.PlanId, cancellationToken)
            ?? throw new TenantsBillingException(TenantsBillingErrorKind.Unprocessable, TenantsBillingErrorCodes.PlanNotFound,
                "Plan not found.");

        var now = _timeProvider.GetUtcNow();
        var period = BillingPeriod.For(now, tenant.TimeZone);
        var usage = await _usageRepository.GetAsync(tenant.Id, period, cancellationToken)
                    ?? TenantUsage.Start(tenant.Id, period);

        if (sourceEventId is not null && await _usageRepository.IsRecordedAsync(sourceEventId, cancellationToken))
            return usage.QuotaStatus;

        var result = usage.Record(eventsConsumed, alertsTriggered, apiRequests, channelUsage, plan, now.UtcDateTime);
        try
        {
            await _usageRepository.SaveAsync(usage, sourceEventId, cancellationToken);
        }
        catch (DuplicateUsageEventException)
        {
            // Another consumer recorded the same event first; its totals are the ones that count.
            return result.Status;
        }

        // Quota events are published once per period and level, when the status escalates.
        if (result.Escalated)
        {
            var eventType = result.Status == TenantQuotaStatus.HardLimit
                ? "TenantQuotaHardLimitReached"
                : "TenantQuotaSoftLimitReached";
            await _eventPublisher.PublishAsync(eventType, new
            {
                tenant.Id,
                eventsConsumed,
                alertsTriggered,
                apiRequests,
                channelUsage,
                SourceEventId = sourceEventId,
                usage.Period,
                Usage = new { usage.EventsConsumed, usage.AlertsTriggered, usage.ApiRequests, usage.ChannelUsage },
                UsagePercentage = Math.Round(result.UsagePercentage, 2)
            }, cancellationToken);
        }

        return result.Status;
    }

    public Task<Tenant?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _tenantRepository.GetByIdAsync(tenantId, cancellationToken);

    public async Task<Tenant> SuspendTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _tenantRepository.GetByIdAsync(tenantId, cancellationToken)
            ?? throw new TenantsBillingException(TenantsBillingErrorKind.NotFound, TenantsBillingErrorCodes.TenantNotFound,
                "Tenant not found.");

        tenant.Suspend();
        await _tenantRepository.UpdateAsync(tenant, cancellationToken);
        await _eventPublisher.PublishAsync("TenantSuspended", new { tenant.Id, tenant.Name, tenant.Status }, cancellationToken);
        return tenant;
    }
}
