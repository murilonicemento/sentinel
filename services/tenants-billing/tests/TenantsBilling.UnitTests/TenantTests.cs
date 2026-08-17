using TenantsBilling.Application.Services;
using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Enums;
using TenantsBilling.Domain.Events;
using TenantsBilling.Domain.Repositories;
using TenantsBilling.Infrastructure.HostedServices;

namespace TenantsBilling.UnitTests;

public class TenantTests
{
    [Fact]
    public void Create_WithValidData_CreatesActiveTenant()
    {
        var tenant = Tenant.Create("Northwind", Guid.NewGuid(), "Brazil", "UTC-03:00");

        Assert.Equal("Northwind", tenant.Name);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal("Brazil", tenant.Region);
        Assert.Equal("UTC-03:00", tenant.TimeZone);
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => Tenant.Create(" ", Guid.NewGuid()));

        Assert.Contains("name", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecordUsage_WhenUsageExceedsSoftLimit_ReturnsSoftLimit()
    {
        var tenant = Tenant.Create("Northwind", Guid.NewGuid());

        var result = tenant.RecordUsage(81, 10, 80, 50);

        Assert.Equal(TenantQuotaStatus.SoftLimit, result);
    }

    [Fact]
    public void RecordUsage_WhenUsageExceedsHardLimit_ReturnsHardLimit()
    {
        var tenant = Tenant.Create("Northwind", Guid.NewGuid());

        var result = tenant.RecordUsage(100, 50, 100, 50);

        Assert.Equal(TenantQuotaStatus.HardLimit, result);
    }
}

public class TenantBillingDeadLetterTests
{
    [Fact]
    public void ShouldReplay_WhenMessageHasRetryFailureAndPayload_IsTrue()
    {
        var deadLetter = new TenantDeadLetterEnvelope(
            "tenant-usage-events",
            "Processing failed after retries",
            2,
            0,
            10,
            "{\"tenantId\":\"11111111-1111-1111-1111-111111111111\"}",
            false,
            string.Empty);

        Assert.True(TenantDeadLetterConsumerHostedService.ShouldReplay(deadLetter));
    }

    [Fact]
    public void ParseDeadLetterEnvelope_WithValidPayload_ReturnsEnvelope()
    {
        var json = "{\"sourceTopic\":\"tenant-usage-events\",\"reason\":\"Processing failed after retries\",\"attempts\":3,\"partition\":0,\"offset\":42,\"originalPayload\":\"{\\\"tenantId\\\":\\\"11111111-1111-1111-1111-111111111111\\\"}\",\"replayRequested\":true,\"replayRequestedBy\":\"dlq-consumer\"}";

        var result = TenantDeadLetterConsumerHostedService.ParseDeadLetterEnvelope(json);

        Assert.NotNull(result);
        Assert.Equal("tenant-usage-events", result!.SourceTopic);
        Assert.Equal("Processing failed after retries", result.Reason);
        Assert.True(result.ReplayRequested);
    }
}

public class TenantManagementServiceTests
{
    [Fact]
    public async Task CreateTenantAsync_WithValidPlan_CreatesTenant()
    {
        var tenantRepository = new InMemoryTenantRepository();
        var planRepository = new InMemoryPlanRepository();
        var eventPublisher = new FakeTenantEventPublisher();
        var service = new TenantManagementService(tenantRepository, planRepository, eventPublisher);
        var plan = new Plan(Guid.NewGuid(), "Pro", 1000, 200, 80, 25);
        await planRepository.AddAsync(plan);

        var result = await service.CreateTenantAsync(new CreateTenantCommand("Acme", plan.Id, "Brazil", "UTC-03:00"));

        Assert.Equal("Acme", result.Name);
        Assert.Equal(plan.Id, result.PlanId);
        Assert.Equal(TenantStatus.Active, result.Status);
        Assert.Contains("TenantCreated", eventPublisher.Events);
    }

    [Fact]
    public async Task SuspendTenantAsync_WhenTenantExists_SuspendsTenant()
    {
        var tenantRepository = new InMemoryTenantRepository();
        var planRepository = new InMemoryPlanRepository();
        var eventPublisher = new FakeTenantEventPublisher();
        var service = new TenantManagementService(tenantRepository, planRepository, eventPublisher);
        var plan = new Plan(Guid.NewGuid(), "Pro", 1000, 200, 80, 25);
        await planRepository.AddAsync(plan);

        var tenant = await service.CreateTenantAsync(new CreateTenantCommand("Contoso", plan.Id));
        var suspended = await service.SuspendTenantAsync(tenant.Id);

        Assert.Equal(TenantStatus.Suspended, suspended.Status);
        Assert.Contains("TenantSuspended", eventPublisher.Events);
    }

    private sealed class InMemoryTenantRepository : ITenantRepository
    {
        private readonly Dictionary<Guid, Tenant> _items = new();

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.TryGetValue(id, out var tenant) ? tenant : null);

        public Task<Tenant?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
        {
            _items[tenant.Id] = tenant;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default)
        {
            _items[tenant.Id] = tenant;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task CreatePlanAsync_WithValidData_CreatesPlan()
    {
        var repository = new InMemoryPlanRepository();
        var publisher = new FakeTenantEventPublisher();
        var service = new PlanManagementService(repository, publisher);

        var plan = await service.CreatePlanAsync(new CreatePlanCommand("Enterprise", 2000, 500, 150, 30));

        Assert.Equal("Enterprise", plan.Name);
        Assert.NotEqual(Guid.Empty, plan.Id);
        Assert.Equal(2, publisher.Events.Count);
    }

    private sealed class InMemoryPlanRepository : IPlanRepository
    {
        private readonly Dictionary<Guid, Plan> _items = new();

        public Task<Plan?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.TryGetValue(id, out var plan) ? plan : null);

        public Task AddAsync(Plan plan, CancellationToken cancellationToken = default)
        {
            _items[plan.Id] = plan;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTenantEventPublisher : ITenantEventPublisher
    {
        public List<string> Events { get; } = new();

        public Task PublishAsync(string eventType, object payload, CancellationToken cancellationToken = default)
        {
            Events.Add(eventType);
            return Task.CompletedTask;
        }
    }
}
