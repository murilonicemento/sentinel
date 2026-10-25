namespace AlertOrchestrator.Application.DTOs;

/// <summary>
/// Tenants whose alert windows the caller may read. Platform scope (trusted services) also sees windows without a
/// tenant, which are opened from region-intersected events.
/// </summary>
public sealed record AlertWindowAccessScope
{
    private AlertWindowAccessScope(bool allTenants, IReadOnlySet<string> tenantIds)
    {
        AllTenants = allTenants;
        TenantIds = tenantIds;
    }

    public bool AllTenants { get; }

    public IReadOnlySet<string> TenantIds { get; }

    public static AlertWindowAccessScope Platform { get; } =
        new(true, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public static AlertWindowAccessScope ForTenants(IEnumerable<string> tenantIds) =>
        new(false, tenantIds.ToHashSet(StringComparer.OrdinalIgnoreCase));

    public bool Allows(string? tenantId) => AllTenants || (tenantId is not null && TenantIds.Contains(tenantId));
}
