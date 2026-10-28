using Microsoft.EntityFrameworkCore;
using Npgsql;
using TenantsBilling.Application.Services;
using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Enums;
using TenantsBilling.Domain.Events;
using TenantsBilling.Domain.Repositories;
using TenantsBilling.Infrastructure.Persistence;
using TenantsBilling.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace TenantsBilling.IntegrationTests.Persistence;

/// <summary>Postgres 15.5 with the tenants_billing schema from platform/init-db/postgres/2-schema.sql.</summary>
public sealed class TenantsBillingPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:15.5").Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Sentinel.sln")))
            directory = directory.Parent;
        var script = await File.ReadAllTextAsync(Path.Combine(directory!.FullName, "platform", "init-db", "postgres",
            "2-schema.sql"));
        var start = script.IndexOf("\\c tenants_billing;", StringComparison.Ordinal) + "\\c tenants_billing;".Length;
        var section = script[start..];
        var next = section.IndexOf("\\c ", StringComparison.Ordinal);

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(next < 0 ? section : section[..next], connection);
        await command.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public TenantsBillingDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TenantsBillingDbContext>().UseNpgsql(ConnectionString).Options);
}

public class TenantUsagePersistenceTests : IClassFixture<TenantsBillingPostgresFixture>
{
    private readonly TenantsBillingPostgresFixture _fixture;

    public TenantUsagePersistenceTests(TenantsBillingPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static TenantManagementService Service(TenantsBillingDbContext context) => new(
        new PostgresTenantRepository(context), new PostgresPlanRepository(context), new NullPublisher(),
        new PostgresTenantUsageRepository(context));

    [Fact]
    public async Task Schema_MatchesTheModel()
    {
        await using var context = _fixture.CreateContext();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task UsageAccumulatesAcrossScopesAndCountsEachEventOnce()
    {
        var plan = new Plan(Guid.NewGuid(), $"plan-{Guid.NewGuid():N}", 100, 100, 100, 100);
        Tenant tenant;
        await using (var context = _fixture.CreateContext())
        {
            await new PostgresPlanRepository(context).AddAsync(plan);
            tenant = await Service(context).CreateTenantAsync(new CreateTenantCommand($"tenant-{Guid.NewGuid():N}",
                plan.Id, TimeZone: "America/Sao_Paulo"));
        }

        foreach (var (amount, eventId) in new[] { (40, "evt-1"), (40, "evt-1"), (45, "evt-2") })
        {
            await using var context = _fixture.CreateContext();
            await Service(context).RegisterUsageAsync(tenant.Id, amount, 0, 0, 0, eventId);
        }

        await using (var context = _fixture.CreateContext())
        {
            var row = await context.TenantUsage.SingleAsync(u => u.TenantId == tenant.Id);
            Assert.Equal(85, row.EventsConsumed);
            Assert.Equal((int)TenantQuotaStatus.SoftLimit, row.QuotaStatus);
            Assert.Equal(2, await context.RecordedUsageEvents.CountAsync(e => e.TenantId == tenant.Id));
        }
    }

    [Fact]
    public async Task SavingAnAlreadyRecordedEvent_RaisesDuplicateUsageEvent()
    {
        var usage = TenantUsage.Start(Guid.NewGuid(), "2026-10");
        await using (var context = _fixture.CreateContext())
            await new PostgresTenantUsageRepository(context).SaveAsync(usage, "evt-dup");

        await using var other = _fixture.CreateContext();
        var repository = new PostgresTenantUsageRepository(other);
        var loaded = await repository.GetAsync(usage.TenantId, "2026-10");

        await Assert.ThrowsAsync<DuplicateUsageEventException>(() => repository.SaveAsync(loaded!, "evt-dup"));
    }

    [Fact]
    public async Task ConcurrentWritersOfTheSamePeriod_Conflict()
    {
        var plan = new Plan(Guid.NewGuid(), "p", 100, 100, 100, 100);
        var usage = TenantUsage.Start(Guid.NewGuid(), "2026-10");
        await using (var context = _fixture.CreateContext())
            await new PostgresTenantUsageRepository(context).SaveAsync(usage, null);

        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();
        var firstRepository = new PostgresTenantUsageRepository(first);
        var secondRepository = new PostgresTenantUsageRepository(second);
        var firstUsage = await firstRepository.GetAsync(usage.TenantId, "2026-10");
        var secondUsage = await secondRepository.GetAsync(usage.TenantId, "2026-10");

        firstUsage!.Record(10, 0, 0, 0, plan, DateTime.UtcNow);
        await firstRepository.SaveAsync(firstUsage, "evt-a");
        secondUsage!.Record(20, 0, 0, 0, plan, DateTime.UtcNow);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondRepository.SaveAsync(secondUsage, "evt-b"));
    }

    private sealed class NullPublisher : ITenantEventPublisher
    {
        public Task PublishAsync(string eventType, object payload, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
