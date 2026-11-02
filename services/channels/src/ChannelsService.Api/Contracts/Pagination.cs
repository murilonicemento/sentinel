namespace ChannelsService.Api.Contracts;

/// <summary>Pagination policy shared by collection endpoints: page starts at 1, default size 10, maximum 50.</summary>
public static class Pagination
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;

    public static bool TryResolve(int? page, int? pageSize, out int resolvedPage, out int resolvedPageSize,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>();
        resolvedPage = page ?? 1;
        resolvedPageSize = pageSize ?? DefaultPageSize;

        if (resolvedPage < 1) errors["page"] = ["The page must be greater than or equal to 1."];
        if (resolvedPageSize is < 1 or > MaxPageSize)
            errors["pageSize"] = [$"The pageSize must be between 1 and {MaxPageSize}."];

        return errors.Count == 0;
    }
}
