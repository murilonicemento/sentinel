namespace Ingestion.Application.DTO;

/// <summary>
/// A tenant's subscription to an external source: the area it covers and the data source that receives what is
/// polled there. The data source must already exist for the tenant, with a measurement type and event permissions
/// that match the source (see docs/services/Ingestion-Service.md, "Polling").
/// </summary>
public sealed record PollingSubscription
{
    public Guid TenantId { get; init; }
    public Guid DataSourceId { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }

    /// <summary>Radius of the area around the point, for area queries (USGS, FIRMS).</summary>
    public double RadiusKm { get; init; } = 100;

    /// <summary>NOAA CDO station id (e.g. <c>GHCND:BR000083743</c>); required by the NOAA sources.</summary>
    public string? StationId { get; init; }
}

/// <summary>Period a poll asks the provider for, in UTC.</summary>
public sealed record PollingWindow(DateTime From, DateTime To);
