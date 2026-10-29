using System.Net;
using System.Text;
using Ingestion.Infrastructure.Write.HttpClients;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ingestion.IntegrationTests.Polling;

/// <summary>USGS GeoJSON responses (format of fdsnws/event/1/query) mapped to collections.</summary>
public class EarthquakePollingHttpClientTests
{
    private const string Response = """
        {
          "type": "FeatureCollection",
          "metadata": { "generated": 1761600000000, "count": 3 },
          "features": [
            { "id": "us7000a", "properties": { "mag": 5.1, "magType": "mww", "time": 1761590000000 },
              "geometry": { "type": "Point", "coordinates": [-71.5, -33.1, 10.0] } },
            { "id": "us7000b", "properties": { "mag": 4.2, "magType": "mb", "time": 1761591000000 } },
            { "id": "us7000c", "geometry": { "type": "Point", "coordinates": [-70.0, -30.0, 5.0] } }
          ]
        }
        """;

    private static EarthquakePollingHttpClient Client(string body)
    {
        var http = new HttpClient(new StubHandler(body)) { BaseAddress = new Uri("https://earthquake.example.com/") };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Polling:Earthquake:DataSourceId"] = Guid.NewGuid().ToString(),
            ["Polling:Earthquake:TenantId"] = Guid.NewGuid().ToString()
        }).Build();
        return new EarthquakePollingHttpClient(http, configuration);
    }

    [Fact]
    public async Task Features_WithoutPropertiesOrGeometry_AreSkipped()
    {
        var collections = await Client(Response).FetchAsync(CancellationToken.None);

        var collection = Assert.Single(collections);
        var sample = Assert.Single(collection.Samples);
        Assert.Equal(5.1, sample.SensorValue);
        Assert.Equal("mww", sample.Unit);
        Assert.Equal((-33.1, -71.5), (sample.Latitude, sample.Longitude));
    }

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }
}
