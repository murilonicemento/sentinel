namespace Ingestion.Application.DTO;

/// <summary>Detected event as stored in the read model, with the tenant that owns the source collection.</summary>
public sealed record DetectedEventDTO(
    Guid EventId,
    Guid CollectionId,
    Guid TenantId,
    string Domain,
    string EventType,
    double Intensity,
    double Latitude,
    double Longitude,
    DateTime CollectedAt);

public sealed record DetectedEventStatisticsDTO
{
    public long TotalEvents { get; init; }
    public required IReadOnlyDictionary<string, int> TotalByType { get; init; }
    public double MinIntensity { get; init; }
    public double MaxIntensity { get; init; }
    public double AverageIntensity { get; init; }

    public static DetectedEventStatisticsDTO Empty { get; } = new() { TotalByType = new Dictionary<string, int>() };
}

public sealed record PagedResultDTO<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalItems { get; init; }
    public int TotalPages { get; init; }

    public static PagedResultDTO<T> Create(IReadOnlyList<T> items, int page, int pageSize, long totalItems) => new()
    {
        Items = items,
        Page = page,
        PageSize = pageSize,
        TotalItems = totalItems,
        TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
    };
}

/// <summary>Tenants whose data the caller may read; platform scope is reserved for trusted services.</summary>
public sealed record TenantAccessScope
{
    private TenantAccessScope(bool allTenants, IReadOnlySet<Guid> tenantIds)
    {
        AllTenants = allTenants;
        TenantIds = tenantIds;
    }

    public bool AllTenants { get; }

    public IReadOnlySet<Guid> TenantIds { get; }

    public static TenantAccessScope Platform { get; } = new(true, new HashSet<Guid>());

    public static TenantAccessScope ForTenants(IEnumerable<Guid> tenantIds) => new(false, tenantIds.ToHashSet());
}
