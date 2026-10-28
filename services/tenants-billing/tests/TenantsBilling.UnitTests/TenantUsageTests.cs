using TenantsBilling.Application.Services;
using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Enums;
using TenantsBilling.Domain.Events;
using TenantsBilling.Domain.Services;
using TenantsBilling.Infrastructure.InMemory;

namespace TenantsBilling.UnitTests;

public class TenantUsageTests
{
    private static readonly Plan Plan = new(Guid.NewGuid(), "Pro", 1000, 200, 5000, 50, softLimitPercentage: 80,
        hardLimitPercentage: 100);

    [Fact]
    public void Record_AccumulatesAndUsesTheHighestPercentageAmongResources()
    {
        var usage = TenantUsage.Start(Guid.NewGuid(), "2026-10");

        var first = usage.Record(100, 10, 0, 0, Plan, DateTime.UtcNow);
        var second = usage.Record(0, 0, 0, 41, Plan, DateTime.UtcNow);

        Assert.Equal(TenantQuotaStatus.WithinLimit, first.Status);
        Assert.Equal(100, usage.EventsConsumed);
        Assert.Equal(TenantQuotaStatus.SoftLimit, second.Status);
        Assert.Equal(82, second.UsagePercentage);
        Assert.True(second.Escalated);
    }

    [Fact]
    public void Record_EscalatesOncePerLevel()
    {
        var usage = TenantUsage.Start(Guid.NewGuid(), "2026-10");

        var soft = usage.Record(850, 0, 0, 0, Plan, DateTime.UtcNow);
        var stillSoft = usage.Record(10, 0, 0, 0, Plan, DateTime.UtcNow);
        var hard = usage.Record(140, 0, 0, 0, Plan, DateTime.UtcNow);

        Assert.True(soft.Escalated);
        Assert.False(stillSoft.Escalated);
        Assert.Equal((TenantQuotaStatus.HardLimit, true), (hard.Status, hard.Escalated));
    }

    [Theory]
    [InlineData("2026-11-01T02:59:00Z", "America/Sao_Paulo", "2026-10")]
    [InlineData("2026-11-01T03:00:00Z", "America/Sao_Paulo", "2026-11")]
    [InlineData("2026-11-01T02:59:00Z", "UTC", "2026-11")]
    [InlineData("2026-11-01T02:59:00Z", "UTC-03:00", "2026-10")]
    [InlineData("2026-11-01T02:59:00Z", null, "2026-10")]
    public void BillingPeriod_IsTheCalendarMonthInTheTenantTimeZone(string instant, string? timeZone, string expected)
    {
        // Unknown or missing zones (such as "UTC-03:00") fall back to America/Sao_Paulo.
        Assert.Equal(expected, BillingPeriod.For(DateTimeOffset.Parse(instant), timeZone));
    }

    [Fact]
    public async Task RegisterUsage_CountsAnEventOnceAndPublishesEachLevelOnce()
    {
        var (service, tenant, publisher) = await ServiceAsync();

        await service.RegisterUsageAsync(tenant.Id, 850, 0, 0, 0, "evt-1");
        await service.RegisterUsageAsync(tenant.Id, 850, 0, 0, 0, "evt-1");
        var status = await service.RegisterUsageAsync(tenant.Id, 10, 0, 0, 0, "evt-2");

        Assert.Equal(TenantQuotaStatus.SoftLimit, status);
        Assert.Equal(["TenantCreated", "TenantQuotaSoftLimitReached"], publisher.Events);
    }

    [Fact]
    public async Task RegisterUsage_StartsANewPeriodEachMonth()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero));
        var (service, tenant, _) = await ServiceAsync(clock);

        await service.RegisterUsageAsync(tenant.Id, 900, 0, 0, 0, "evt-oct");
        clock.Now = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var status = await service.RegisterUsageAsync(tenant.Id, 10, 0, 0, 0, "evt-nov");

        Assert.Equal(TenantQuotaStatus.WithinLimit, status);
    }

    [Fact]
    public async Task RegisterUsage_ForASuspendedTenant_IsHardLimitWithoutQuotaEvents()
    {
        var (service, tenant, publisher) = await ServiceAsync();
        await service.SuspendTenantAsync(tenant.Id);

        var status = await service.RegisterUsageAsync(tenant.Id, 1, 0, 0, 0, "evt-1");

        Assert.Equal(TenantQuotaStatus.HardLimit, status);
        Assert.DoesNotContain(publisher.Events, e => e.StartsWith("TenantQuota", StringComparison.Ordinal));
    }

    private static async Task<(TenantManagementService, Tenant, RecordingPublisher)> ServiceAsync(
        TimeProvider? clock = null)
    {
        var plans = new InMemoryPlanRepository();
        await plans.AddAsync(Plan);
        var publisher = new RecordingPublisher();
        var service = new TenantManagementService(new InMemoryTenantRepository(), plans, publisher,
            new InMemoryTenantUsageRepository(), clock);
        var tenant = await service.CreateTenantAsync(new CreateTenantCommand($"tenant-{Guid.NewGuid():N}", Plan.Id,
            TimeZone: "America/Sao_Paulo"));
        return (service, tenant, publisher);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingPublisher : ITenantEventPublisher
    {
        public List<string> Events { get; } = [];

        public Task PublishAsync(string eventType, object payload, CancellationToken cancellationToken = default)
        {
            Events.Add(eventType);
            return Task.CompletedTask;
        }
    }
}
