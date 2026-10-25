namespace RiskCatalog.Application.DTO;

public sealed record PagedResultDTO<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalItems { get; init; }
    public int TotalPages { get; init; }

    public static PagedResultDTO<T> From(IReadOnlyList<T> orderedItems, int page, int pageSize) => new()
    {
        Items = orderedItems.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
        Page = page,
        PageSize = pageSize,
        TotalItems = orderedItems.Count,
        TotalPages = (int)Math.Ceiling(orderedItems.Count / (double)pageSize)
    };
}
