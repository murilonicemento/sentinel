using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Enums;

namespace TenantsBilling.Application.DTOs;

public sealed record TenantDTO(
    Guid Id,
    string Name,
    Guid PlanId,
    string Status,
    string Region,
    string TimeZone,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static TenantDTO From(Tenant tenant) => new(
        tenant.Id,
        tenant.Name,
        tenant.PlanId,
        tenant.Status.ToString(),
        tenant.Region,
        tenant.TimeZone,
        tenant.CreatedAt,
        tenant.UpdatedAt);
}

public sealed record PlanDTO(
    Guid Id,
    string Name,
    int MaxEventsPerMonth,
    int MaxAlertsPerMonth,
    int MaxApiRequestsPerMonth,
    int MaxChannelsPerMonth,
    int SoftLimitPercentage,
    int HardLimitPercentage,
    bool IsActive)
{
    public static PlanDTO From(Plan plan) => new(
        plan.Id,
        plan.Name,
        plan.MaxEventsPerMonth,
        plan.MaxAlertsPerMonth,
        plan.MaxApiRequestsPerMonth,
        plan.MaxChannelsPerMonth,
        plan.SoftLimitPercentage,
        plan.HardLimitPercentage,
        plan.IsActive);
}

public sealed record UsageResultDTO(Guid TenantId, string QuotaStatus)
{
    public static UsageResultDTO From(Guid tenantId, TenantQuotaStatus status) => new(tenantId, status.ToString());
}

public sealed record ResponseBaseDTO<T>
{
    public int StatusCode { get; init; }
    public bool Success { get; init; }
    public required T Data { get; init; }
}
