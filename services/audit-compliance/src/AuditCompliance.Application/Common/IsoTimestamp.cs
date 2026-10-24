using System.Globalization;
using System.Text.RegularExpressions;

namespace AuditCompliance.Application.Common;

public static partial class IsoTimestamp
{
    /// <summary>
    /// Aceita somente timestamps ISO 8601 com offset explícito (Z ou ±hh:mm), evitando que valores sem
    /// offset sejam interpretados no fuso horário local do servidor.
    /// </summary>
    public static bool TryParse(string? value, out DateTimeOffset result)
    {
        result = default;

        return !string.IsNullOrWhiteSpace(value) &&
               ExplicitOffsetPattern().IsMatch(value) &&
               DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T.+(Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex ExplicitOffsetPattern();
}
