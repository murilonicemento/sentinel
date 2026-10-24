using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Events;
using TenantsBilling.Domain.Repositories;

namespace TenantsBilling.Application.Services;

public sealed record CreatePlanCommand(
    string Name,
    int MaxEventsPerMonth,
    int MaxAlertsPerMonth,
    int MaxApiRequestsPerMonth,
    int MaxChannelsPerMonth,
    int SoftLimitPercentage = 80,
    int HardLimitPercentage = 100);

public sealed class PlanManagementService
{
    private readonly IPlanRepository _planRepository;
    private readonly ITenantEventPublisher _eventPublisher;

    public PlanManagementService(IPlanRepository planRepository, ITenantEventPublisher eventPublisher)
    {
        _planRepository = planRepository;
        _eventPublisher = eventPublisher;
    }

    public async Task<Plan> CreatePlanAsync(CreatePlanCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Plan name is required.", nameof(command));

        var plan = new Plan(
            Guid.NewGuid(),
            command.Name,
            command.MaxEventsPerMonth,
            command.MaxAlertsPerMonth,
            command.MaxApiRequestsPerMonth,
            command.MaxChannelsPerMonth,
            command.SoftLimitPercentage,
            command.HardLimitPercentage);

        await _planRepository.AddAsync(plan, cancellationToken);
        await _eventPublisher.PublishAsync("PlanCreated", new { plan.Id, plan.Name, plan.MaxEventsPerMonth }, cancellationToken);
        await _eventPublisher.PublishAsync("PlanPolicyUpdated", new { plan.Id, plan.SoftLimitPercentage, plan.HardLimitPercentage }, cancellationToken);

        return plan;
    }
}
