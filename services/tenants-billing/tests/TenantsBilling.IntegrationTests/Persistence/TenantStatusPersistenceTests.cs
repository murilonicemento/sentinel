using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Enums;
using TenantsBilling.Infrastructure.Persistence.Repositories;

namespace TenantsBilling.IntegrationTests.Persistence;

public class TenantStatusPersistenceTests : IClassFixture<TenantsBillingPostgresFixture>
{
    private readonly TenantsBillingPostgresFixture _fixture;

    public TenantStatusPersistenceTests(TenantsBillingPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<Tenant> StoredTenantAsync(string? name = null, TenantStatus status = TenantStatus.Active)
    {
        var plan = new Plan(Guid.NewGuid(), $"plan-{Guid.NewGuid():N}", 100, 100, 100, 100);
        var tenant = Tenant.Rehydrate(Guid.NewGuid(), name ?? $"tenant-{Guid.NewGuid():N}", plan.Id, status, "global",
            "UTC", DateTime.UtcNow, DateTime.UtcNow);
        await using var context = _fixture.CreateContext();
        await new PostgresPlanRepository(context).AddAsync(plan);
        await new PostgresTenantRepository(context).AddAsync(tenant);
        return tenant;
    }

    [Fact]
    public async Task UpdateStatus_AppliesOnlyWhileTheStoredStatusIsTheExpectedOne()
    {
        var stored = await StoredTenantAsync();

        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();
        var firstRepository = new PostgresTenantRepository(first);
        var secondRepository = new PostgresTenantRepository(second);
        var cancel = await firstRepository.GetByIdAsync(stored.Id);
        var suspend = await secondRepository.GetByIdAsync(stored.Id);

        cancel!.ChangeStatus(TenantStatus.Canceled);
        Assert.True(await firstRepository.UpdateStatusAsync(cancel, TenantStatus.Active));
        suspend!.ChangeStatus(TenantStatus.Suspended);
        Assert.False(await secondRepository.UpdateStatusAsync(suspend, TenantStatus.Active));

        await using var reader = _fixture.CreateContext();
        Assert.Equal(TenantStatus.Canceled, (await new PostgresTenantRepository(reader).GetByIdAsync(stored.Id))!.Status);
    }

    [Fact]
    public async Task List_FiltersByTenantsAndStatusAndPagesInNameOrder()
    {
        var prefix = Guid.NewGuid().ToString("N")[..8];
        var charlie = await StoredTenantAsync($"{prefix}-charlie");
        var alpha = await StoredTenantAsync($"{prefix}-alpha");
        var bravo = await StoredTenantAsync($"{prefix}-bravo", TenantStatus.Suspended);
        await StoredTenantAsync($"{prefix}-not-listed");
        var ids = new[] { charlie.Id, alpha.Id, bravo.Id };

        await using var context = _fixture.CreateContext();
        var repository = new PostgresTenantRepository(context);
        var firstPage = await repository.ListAsync(ids, null, 1, 2);
        var secondPage = await repository.ListAsync(ids, null, 2, 2);
        var active = await repository.ListAsync(ids, TenantStatus.Active, 1, 10);

        Assert.Equal([alpha.Id, bravo.Id], firstPage.Items.Select(x => x.Id));
        Assert.Equal([charlie.Id], secondPage.Items.Select(x => x.Id));
        Assert.Equal(3, firstPage.TotalItems);
        Assert.Equal([alpha.Id, charlie.Id], active.Items.Select(x => x.Id));
        Assert.Equal(2, active.TotalItems);
    }

    [Fact]
    public async Task ListPlans_IsOrderedByName()
    {
        var prefix = $"aaa-{Guid.NewGuid():N}";
        await using (var context = _fixture.CreateContext())
        {
            var plans = new PostgresPlanRepository(context);
            await plans.AddAsync(new Plan(Guid.NewGuid(), $"{prefix}-b", 1, 1, 1, 1));
            await plans.AddAsync(new Plan(Guid.NewGuid(), $"{prefix}-a", 1, 1, 1, 1));
        }

        await using var reader = _fixture.CreateContext();
        var repository = new PostgresPlanRepository(reader);
        var all = new List<Plan>();
        for (var number = 1; ; number++)
        {
            var page = await repository.ListAsync(number, 50);
            all.AddRange(page.Items);
            if (page.Items.Count == 0 || all.Count >= page.TotalItems) break;
        }

        Assert.Equal([$"{prefix}-a", $"{prefix}-b"], all.Select(x => x.Name).Where(name => name.StartsWith(prefix)));
    }
}
