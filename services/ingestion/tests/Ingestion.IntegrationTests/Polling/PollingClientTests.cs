using System.Net;
using System.Text;
using Ingestion.Application.DTO;
using Ingestion.Infrastructure.Write.HttpClients;
using Xunit;

namespace Ingestion.IntegrationTests.Polling;

/// <summary>What each polling client asks its provider and what it returns, from representative responses.</summary>
public class PollingClientTests
{
    private static readonly PollingWindow Window =
        new(new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc));

    private static readonly PollingSubscription Subscription = new()
    {
        TenantId = Guid.NewGuid(),
        DataSourceId = Guid.NewGuid(),
        Latitude = -23.55,
        Longitude = -46.63,
        RadiusKm = 50,
        StationId = "GHCND:BR000083781"
    };

    private static (HttpClient Client, StubHandler Handler) Http(string body,
        HttpStatusCode status = HttpStatusCode.OK, string baseUrl = "https://provider.example.com/v2/")
    {
        var handler = new StubHandler(body, status);
        return (new HttpClient(handler) { BaseAddress = new Uri(baseUrl) }, handler);
    }

    [Fact]
    public async Task Earthquake_AsksForTheRadiusAndWindow_AndSkipsFeaturesWithoutPosition()
    {
        const string body = """
            { "type": "FeatureCollection", "features": [
              { "id": "us7000a", "properties": { "mag": 5.1, "magType": "mww", "time": 1793146200000 },
                "geometry": { "type": "Point", "coordinates": [-46.7, -23.6, 10.0] } },
              { "id": "us7000b", "properties": { "mag": 4.2, "time": 1793147000000 } },
              { "id": "us7000c", "properties": { "time": 1793148000000 },
                "geometry": { "type": "Point", "coordinates": [-46.0, -23.0, 5.0] } } ] }
            """;
        var (http, handler) = Http(body);

        var readings = await new EarthquakePollingHttpClient(http).FetchAsync(Subscription, Window, default);

        Assert.Contains("latitude=-23.55&longitude=-46.63&maxradiuskm=50", handler.LastUri!.Query);
        Assert.Contains("starttime=2026-10-27T00:00:00&endtime=2026-10-28T00:00:00", handler.LastUri.Query);
        var reading = Assert.Single(readings);
        Assert.Equal("us7000a", reading.SourceEventId);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1793146200000).UtcDateTime, reading.CollectedAt);
        Assert.Equal(("Mw", 5.1), (reading.Samples[0].Unit, reading.Samples[0].SensorValue));
    }

    [Fact]
    public async Task Fire_AsksForTheBoundingBox_AndKeepsDetectionsInsideTheWindow()
    {
        const string csv = """
            latitude,longitude,bright_ti4,scan,track,acq_date,acq_time,satellite,instrument,confidence,version,bright_ti5,frp,daynight
            -23.5,-46.6,330.1,0.4,0.4,2026-10-27,1530,N,VIIRS,n,2.0NRT,290.2,12.5,D
            -23.4,-46.5,331.0,0.4,0.4,2026-10-25,0105,N,VIIRS,n,2.0NRT,291.0,8.0,N
            """;
        var (http, handler) = Http(csv, baseUrl: "https://firms.example.com/");

        var readings = await new FirePollingHttpClient(http, "map-key-for-tests").FetchAsync(Subscription, Window, default);

        Assert.StartsWith("/api/area/csv/map-key-for-tests/VIIRS_SNPP_NRT/", handler.LastUri!.AbsolutePath);
        Assert.EndsWith("/1", handler.LastUri.AbsolutePath);
        var reading = Assert.Single(readings);
        Assert.Equal(new DateTime(2026, 10, 27, 15, 30, 0, DateTimeKind.Utc), reading.CollectedAt);
        Assert.Equal("N:-23.5:-46.6:2026-10-27:1530", reading.SourceEventId);
        Assert.Equal(("MW", 12.5), (reading.Samples[0].Unit, reading.Samples[0].SensorValue));
    }

    private const string NoaaBody = """
        { "results": [
          { "date": "2026-10-26T00:00:00", "datatype": "PRCP", "station": "GHCND:BR000083781", "value": 12.4 },
          { "date": "2026-10-27T00:00:00", "datatype": "PRCP", "station": "GHCND:BR000083781", "value": 30.1 },
          { "date": "2026-10-27T00:00:00", "datatype": "TMAX", "station": "GHCND:BR000083781", "value": 31.0 } ] }
        """;

    [Fact]
    public async Task Flood_AsksForTheStationInMetricUnits_AndEmitsOneReadingPerDay()
    {
        var (http, handler) = Http(NoaaBody);

        var readings = await new FloodPollingHttpClient(http).FetchAsync(Subscription, Window, default);

        Assert.Equal("/v2/data", handler.LastUri!.AbsolutePath);
        Assert.Contains("stationid=GHCND%3ABR000083781", handler.LastUri.Query);
        Assert.Contains("startdate=2026-10-27&enddate=2026-10-28&units=metric", handler.LastUri.Query);
        Assert.Equal(["GHCND:BR000083781:PRCP:2026-10-26", "GHCND:BR000083781:PRCP:2026-10-27"],
            readings.Select(r => r.SourceEventId));
        Assert.All(readings, r => Assert.Equal("mm", r.Samples[0].Unit));
    }

    [Fact]
    public async Task Landslide_AccumulatesThePrecipitationOfTheWindow()
    {
        var (http, _) = Http(NoaaBody);

        var reading = Assert.Single(await new LandslidePollingHttpClient(http).FetchAsync(Subscription, Window, default));

        Assert.Equal(42.5, reading.Samples[0].SensorValue, 3);
        Assert.Equal("GHCND:BR000083781:PRCP-ACCUMULATED:2026-10-27:2026-10-28", reading.SourceEventId);
    }

    [Fact]
    public async Task Rainfall_PrefersStormglassValues_AndIgnoresForecastHours()
    {
        const string body = """
            { "hours": [
              { "time": "2026-10-27T10:00:00+00:00", "precipitation": { "noaa": 3.0, "sg": 4.5 } },
              { "time": "2026-10-27T11:00:00+00:00", "precipitation": { "icon": 2.0 } },
              { "time": "2026-10-28T10:00:00+00:00", "precipitation": { "sg": 9.0 } } ],
              "meta": { "lat": -23.55, "lng": -46.63 } }
            """;
        var (http, handler) = Http(body);

        var readings = await new RainfallPollingHttpClient(http).FetchAsync(Subscription, Window, default);

        Assert.Equal("/v2/weather/point", handler.LastUri!.AbsolutePath);
        Assert.Contains("params=precipitation&start=1793059200&end=1793145600", handler.LastUri.Query);
        Assert.Equal([4.5, 2.0], readings.Select(r => r.Samples[0].SensorValue));
        Assert.Equal("precipitation:-23.55:-46.63:2026102710", readings[0].SourceEventId);
    }

    [Fact]
    public async Task ProviderError_FailsThePoll()
    {
        var (http, _) = Http("quota exceeded", HttpStatusCode.PaymentRequired);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new WindGustPollingHttpClient(http).FetchAsync(Subscription, Window, default));
    }

    internal sealed class StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
