namespace TenantsBilling.Domain.Services;

/// <summary>Billing periods are calendar months in the tenant's time zone.</summary>
public static class BillingPeriod
{
    /// <summary>Used when the tenant has no time zone or an unknown one.</summary>
    public const string DefaultTimeZoneId = "America/Sao_Paulo";

    public static string For(DateTimeOffset instant, string? timeZoneId)
    {
        var local = TimeZoneInfo.ConvertTime(instant, Resolve(timeZoneId));
        return $"{local.Year:D4}-{local.Month:D2}";
    }

    private static TimeZoneInfo Resolve(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone))
            return zone;

        return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);
    }
}
