namespace TenantsBilling.Domain.Repositories;

/// <summary>One page of a listing and the number of items across all pages.</summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, int TotalItems);
