using Ingestion.Application.DTO;

namespace Ingestion.Infrastructure.Write.Options;

/// <summary>
/// One external source, section <c>Polling:&lt;Source&gt;</c>. A disabled source is never called and needs no other
/// setting; an enabled one is validated at startup. <see cref="ApiKey"/> is a secret: User Secrets locally,
/// <c>platform/.env</c> in compose, never <c>appsettings*.json</c>.
/// </summary>
public sealed class PollingSourceOptions
{
    public bool Enabled { get; init; }
    public string? BaseUrl { get; init; }

    /// <summary>FIRMS map key, NOAA CDO token or Stormglass API key, depending on the source.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Time between polls. Mind the provider quota: Stormglass's free plan allows 10 requests a day.</summary>
    public int IntervalMinutes { get; init; } = 60;

    /// <summary>How far back each poll asks for; overlapping windows are deduplicated by the provider's event id.</summary>
    public int LookbackHours { get; init; } = 24;

    public List<PollingSubscription> Subscriptions { get; init; } = [];
}

public static class PollingSources
{
    public const string Fire = "Fire";
    public const string Earthquake = "Earthquake";
    public const string Flood = "Flood";
    public const string Landslide = "Landslide";
    public const string TemperatureAnomaly = "TemperatureAnomaly";
    public const string HumidityAnomaly = "HumidityAnomaly";
    public const string WindGust = "WindGust";
    public const string Rainfall = "Rainfall";
    public const string PressureChange = "PressureChange";

    public static readonly string[] All =
        [Fire, Earthquake, Flood, Landslide, TemperatureAnomaly, HumidityAnomaly, WindGust, Rainfall, PressureChange];

    /// <summary>Sources whose provider (NOAA CDO) is queried by weather station.</summary>
    public static readonly string[] ByStation = [Flood, Landslide, TemperatureAnomaly, HumidityAnomaly];

    /// <summary>Sources that need <see cref="PollingSourceOptions.ApiKey"/> (all but USGS).</summary>
    public static bool RequiresApiKey(string source) => source != Earthquake;

    /// <summary>Errors of an enabled source, each naming its configuration key; empty when valid.</summary>
    public static IEnumerable<string> Validate(string source, PollingSourceOptions options)
    {
        if (!options.Enabled) yield break;

        var section = $"Polling:{source}";
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
            yield return $"{section}:BaseUrl must be an absolute URL.";
        if (RequiresApiKey(source) && string.IsNullOrWhiteSpace(options.ApiKey))
            yield return $"{section}:ApiKey is required.";
        if (options.IntervalMinutes < 1)
            yield return $"{section}:IntervalMinutes must be at least 1.";
        if (options.LookbackHours < 1)
            yield return $"{section}:LookbackHours must be at least 1.";
        if (options.Subscriptions.Count == 0)
            yield return $"{section}:Subscriptions requires at least one subscription.";

        for (var index = 0; index < options.Subscriptions.Count; index++)
        {
            var subscription = options.Subscriptions[index];
            var key = $"{section}:Subscriptions:{index}";
            if (subscription.TenantId == Guid.Empty) yield return $"{key}:TenantId is required.";
            if (subscription.DataSourceId == Guid.Empty) yield return $"{key}:DataSourceId is required.";
            if (subscription.Latitude is < -90 or > 90) yield return $"{key}:Latitude must be between -90 and 90.";
            if (subscription.Longitude is < -180 or > 180)
                yield return $"{key}:Longitude must be between -180 and 180.";
            if (subscription.RadiusKm is <= 0 or > 20_000) yield return $"{key}:RadiusKm must be between 0 and 20000.";
            if (ByStation.Contains(source) && string.IsNullOrWhiteSpace(subscription.StationId))
                yield return $"{key}:StationId is required by the NOAA sources.";
        }
    }
}
