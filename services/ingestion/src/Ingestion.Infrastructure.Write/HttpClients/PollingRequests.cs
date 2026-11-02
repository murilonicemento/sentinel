using System.Globalization;
using System.Text.Json;
using Ingestion.Application.DTO;

namespace Ingestion.Infrastructure.Write.HttpClients;

/// <summary>Helpers shared by the polling clients.</summary>
internal static class PollingRequests
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static string Number(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>Bounding box (west, south, east, north) of the subscription's circle, clamped to valid ranges.</summary>
    public static (double West, double South, double East, double North) BoundingBox(PollingSubscription area)
    {
        const double kmPerDegree = 111.32;
        var latDelta = area.RadiusKm / kmPerDegree;
        var cos = Math.Max(Math.Cos(area.Latitude * Math.PI / 180), 0.01);
        var lonDelta = Math.Min(area.RadiusKm / (kmPerDegree * cos), 180);
        return (Math.Max(area.Longitude - lonDelta, -180), Math.Max(area.Latitude - latDelta, -90),
            Math.Min(area.Longitude + lonDelta, 180), Math.Min(area.Latitude + latDelta, 90));
    }

    /// <summary>Reads a successful response; a provider error fails the poll so it is logged and counted.</summary>
    public static async Task<string> ReadAsync(HttpResponseMessage response, string source,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"{source} answered {(int)response.StatusCode}.", null,
                response.StatusCode);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
