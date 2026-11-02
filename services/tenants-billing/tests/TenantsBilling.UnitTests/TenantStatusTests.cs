using TenantsBilling.Application.Errors;
using TenantsBilling.Application.Services;
using TenantsBilling.Domain.Aggregates;
using TenantsBilling.Domain.Enums;
using TenantsBilling.Domain.Events;
using TenantsBilling.Domain.Repositories;
using TenantsBilling.Infrastructure.InMemory;

namespace TenantsBilling.UnitTests;

public class TenantStatusTransitionTests
{
    [Theory]
    [InlineData(TenantStatus.Active, TenantStatus.Suspended, true)]
    [InlineData(TenantStatus.Active, TenantStatus.Canceled, true)]
    [InlineData(TenantStatus.Active, TenantStatus.Active, true)]
    [InlineData(TenantStatus.Active, TenantStatus.Pending, false)]
    [InlineData(TenantStatus.Suspended, TenantStatus.Active, true)]
    [InlineData(TenantStatus.Suspended, TenantStatus.Canceled, true)]
    [InlineData(TenantStatus.Pending, TenantStatus.Active, true)]
    [InlineData(TenantStatus.Pending, TenantStatus.Suspended, false)]
    [InlineData(TenantStatus.Canceled, TenantStatus.Active, false)]
    [InlineData(TenantStatus.Canceled, TenantStatus.Suspended, false)]
    [InlineData(TenantStatus.Canceled, TenantStatus.Canceled, true)]
    public void CanChangeStatusTo_FollowsTheLifecycle(TenantStatus from, TenantStatus to, bool allowed)
    {
        var tenant = TenantWith(from);

        Assert.Equal(allowed, tenant.CanChangeStatusTo(to));
    }

    [Fact]
    public void ChangeStatus_ToTheCurrentStatus_ChangesNothing()
    {
        var tenant = TenantWith(TenantStatus.Suspended);
        var updatedAt = tenant.UpdatedAt;

        Assert.False(tenant.ChangeStatus(TenantStatus.Suspended));
        Assert.Equal(updatedAt, tenant.UpdatedAt);
    }

    [Fact]
    public void ChangeStatus_FromCanceled_Throws()
    {
        var tenant = TenantWith(TenantStatus.Canceled);

        Assert.Throws<InvalidOperationException>(() => tenant.ChangeStatus(TenantStatus.Active));
        Assert.Equal(TenantStatus.Canceled, tenant.Status);
    }

    internal static Tenant TenantWith(TenantStatus status) => Tenant.Rehydrate(Guid.NewGuid(), "Acme", Guid.NewGuid(),
        status, "global", "UTC", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1));
}

public class TenantStatusServiceTests
{
    private readonly InMemoryTenantRepository _tenants = new();
    private readonly RecordingPublisher _publisher = new();

    private TenantManagementService Service(ITenantRepository? tenants = null) =>
        new(tenants ?? _tenants, new InMemoryPlanRepository(), _publisher, new InMemoryTenantUsageRepository());

    private async Task<Tenant> StoredAsync(TenantStatus status)
    {
        var tenant = TenantStatusTransitionTests.TenantWith(status);
        await _tenants.AddAsync(tenant);
        return tenant;
    }

    [Theory]
    [InlineData(TenantStatus.Active, TenantStatus.Suspended, "TenantSuspended")]
    [InlineData(TenantStatus.Suspended, TenantStatus.Active, "TenantReactivated")]
    [InlineData(TenantStatus.Suspended, TenantStatus.Canceled, "TenantCanceled")]
    public async Task ChangeStatusAsync_SavesTheStatusAndPublishesItsEvent(TenantStatus from, TenantStatus to,
        string expectedEvent)
    {
        var tenant = await StoredAsync(from);

        var changed = await Service().ChangeStatusAsync(tenant.Id, to);

        Assert.Equal(to, changed.Status);
        Assert.Equal(to, (await _tenants.GetByIdAsync(tenant.Id))!.Status);
        Assert.Equal([expectedEvent], _publisher.Events);
    }

    [Fact]
    public async Task ChangeStatusAsync_ToTheCurrentStatus_PublishesNothing()
    {
        var tenant = await StoredAsync(TenantStatus.Suspended);

        var result = await Service().ChangeStatusAsync(tenant.Id, TenantStatus.Suspended);

        Assert.Equal(TenantStatus.Suspended, result.Status);
        Assert.Empty(_publisher.Events);
    }

    [Fact]
    public async Task ChangeStatusAsync_OfACanceledTenant_IsANotAllowedTransition()
    {
        var tenant = await StoredAsync(TenantStatus.Canceled);

        var error = await Assert.ThrowsAsync<TenantsBillingException>(() =>
            Service().ChangeStatusAsync(tenant.Id, TenantStatus.Active));

        Assert.Equal(TenantsBillingErrorKind.Conflict, error.Kind);
        Assert.Equal(TenantsBillingErrorCodes.TenantStatusTransitionNotAllowed, error.Code);
        Assert.Equal(TenantStatus.Canceled, (await _tenants.GetByIdAsync(tenant.Id))!.Status);
        Assert.Empty(_publisher.Events);
    }

    [Fact]
    public async Task ChangeStatusAsync_OfAnUnknownTenant_IsNotFound()
    {
        var error = await Assert.ThrowsAsync<TenantsBillingException>(() =>
            Service().ChangeStatusAsync(Guid.NewGuid(), TenantStatus.Suspended));

        Assert.Equal(TenantsBillingErrorCodes.TenantNotFound, error.Code);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenAnotherRequestCanceledTheTenantFirst_IsAConflictAndKeepsCanceled()
    {
        var tenant = await StoredAsync(TenantStatus.Active);
        // The other request cancels the tenant between this request's read and its write.
        var racing = new CancelBeforeWriteRepository(_tenants);

        var error = await Assert.ThrowsAsync<TenantsBillingException>(() =>
            Service(racing).ChangeStatusAsync(tenant.Id, TenantStatus.Suspended));

        Assert.Equal(TenantsBillingErrorCodes.TenantStatusConflict, error.Code);
        Assert.Equal(TenantStatus.Canceled, (await _tenants.GetByIdAsync(tenant.Id))!.Status);
        Assert.Empty(_publisher.Events);
    }

    [Fact]
    public async Task ListTenantsAsync_FiltersByTenantAndStatusAndOrdersByName()
    {
        var zeta = Tenant.Rehydrate(Guid.NewGuid(), "Zeta", Guid.NewGuid(), TenantStatus.Active, "global", "UTC",
            DateTime.UtcNow, DateTime.UtcNow);
        var alpha = Tenant.Rehydrate(Guid.NewGuid(), "Alpha", Guid.NewGuid(), TenantStatus.Active, "global", "UTC",
            DateTime.UtcNow, DateTime.UtcNow);
        var suspended = Tenant.Rehydrate(Guid.NewGuid(), "Beta", Guid.NewGuid(), TenantStatus.Suspended, "global",
            "UTC", DateTime.UtcNow, DateTime.UtcNow);
        var other = Tenant.Rehydrate(Guid.NewGuid(), "Other", Guid.NewGuid(), TenantStatus.Active, "global", "UTC",
            DateTime.UtcNow, DateTime.UtcNow);
        foreach (var tenant in new[] { zeta, alpha, suspended, other }) await _tenants.AddAsync(tenant);

        var visible = new[] { zeta.Id, alpha.Id, suspended.Id };
        var all = await Service().ListTenantsAsync(visible, null, 1, 10);
        var active = await Service().ListTenantsAsync(visible, TenantStatus.Active, 1, 10);
        var secondPage = await Service().ListTenantsAsync(visible, null, 2, 2);

        Assert.Equal(["Alpha", "Beta", "Zeta"], all.Items.Select(x => x.Name));
        Assert.Equal(3, all.TotalItems);
        Assert.Equal(["Alpha", "Zeta"], active.Items.Select(x => x.Name));
        Assert.Equal(["Zeta"], secondPage.Items.Select(x => x.Name));
        Assert.Equal(3, secondPage.TotalItems);
    }

    private sealed class CancelBeforeWriteRepository(InMemoryTenantRepository inner) : ITenantRepository
    {
        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            inner.GetByIdAsync(id, cancellationToken);

        public Task<Tenant?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
            inner.GetByNameAsync(name, cancellationToken);

        public Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default) =>
            inner.AddAsync(tenant, cancellationToken);

        public Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default) =>
            inner.UpdateAsync(tenant, cancellationToken);

        public async Task<bool> UpdateStatusAsync(Tenant tenant, TenantStatus expectedStatus,
            CancellationToken cancellationToken = default)
        {
            var other = (await inner.GetByIdAsync(tenant.Id, cancellationToken))!;
            other.ChangeStatus(TenantStatus.Canceled);
            await inner.UpdateStatusAsync(other, expectedStatus, cancellationToken);

            return await inner.UpdateStatusAsync(tenant, expectedStatus, cancellationToken);
        }

        public Task<PagedList<Tenant>> ListAsync(IReadOnlyCollection<Guid>? tenantIds, TenantStatus? status, int page,
            int pageSize, CancellationToken cancellationToken = default) =>
            inner.ListAsync(tenantIds, status, page, pageSize, cancellationToken);
    }

    private sealed class RecordingPublisher : ITenantEventPublisher
    {
        public List<string> Events { get; } = new();

        public Task PublishAsync(string eventType, object payload, CancellationToken cancellationToken = default)
        {
            Events.Add(eventType);
            return Task.CompletedTask;
        }
    }
}
