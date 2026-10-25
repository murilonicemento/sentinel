namespace ChannelsService.Application.DTOs;

/// <summary>Tenants whose data the caller may read; platform scope is reserved for trusted services.</summary>
public sealed record TenantAccessScope
{
    private TenantAccessScope(bool allTenants, IReadOnlySet<string> tenantIds)
    {
        AllTenants = allTenants;
        TenantIds = tenantIds;
    }

    public bool AllTenants { get; }

    public IReadOnlySet<string> TenantIds { get; }

    public static TenantAccessScope Platform { get; } =
        new(true, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public static TenantAccessScope ForTenants(IEnumerable<string> tenantIds) =>
        new(false, tenantIds.ToHashSet(StringComparer.OrdinalIgnoreCase));

    public bool Allows(string? tenantId) =>
        AllTenants || (!string.IsNullOrWhiteSpace(tenantId) && TenantIds.Contains(tenantId));
}
