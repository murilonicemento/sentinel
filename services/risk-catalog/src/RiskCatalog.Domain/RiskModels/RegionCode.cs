using System.Text.RegularExpressions;

namespace RiskCatalog.Domain.RiskModels;

/// <summary>
/// Business identity of a region (RC-14): two regional parameters are the same region when their codes match,
/// ignoring case. Examples: an IBGE municipality code (<c>3550308</c>) or a slug (<c>sp-capital</c>).
/// </summary>
public static partial class RegionCode
{
    public const int MaxLength = 64;

    /// <summary>Code given to regions created without one (legacy route and rows that existed before RC-14).</summary>
    public static string ForLegacy(Guid regionId) => $"legacy-{regionId:N}";

    /// <summary>Trims and lowercases; valid codes have 1 to 64 letters, digits, '.', '_', ':' or '-'.</summary>
    public static bool TryNormalize(string? value, out string code)
    {
        code = (value ?? string.Empty).Trim().ToLowerInvariant();
        return Pattern().IsMatch(code);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9._:-]{0,63}$")]
    private static partial Regex Pattern();
}
