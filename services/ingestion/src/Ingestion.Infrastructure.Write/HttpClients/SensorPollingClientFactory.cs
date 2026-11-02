using Ingestion.Application.Interfaces.HttpClients;
using Microsoft.Extensions.Configuration;

namespace Ingestion.Infrastructure.Write.HttpClients;

public class SensorPollingClientFactory : ISensorPollingClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public SensorPollingClientFactory(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public IEnumerable<ISensorPollingClient> GetAllClients()
    {
        yield return new FirePollingHttpClient(_httpClientFactory.CreateClient("Fire"), _configuration);
        yield return new EarthquakePollingHttpClient(_httpClientFactory.CreateClient("Earthquake"), _configuration);
        yield return new FloodPollingHttpClient(_httpClientFactory.CreateClient("Flood"), _configuration);
        yield return new LandslidePollingHttpClient(_httpClientFactory.CreateClient("Landslide"), _configuration);
        yield return new TemperatureAnomalyPollingHttpClient(_httpClientFactory.CreateClient("TemperatureAnomaly"), _configuration);
        yield return new HumidityAnomalyPollingHttpClient(_httpClientFactory.CreateClient("HumidityAnomaly"), _configuration);
        yield return new WindGustPollingHttpClient(_httpClientFactory.CreateClient("WindGust"), _configuration);
        yield return new RainfallPollingHttpClient(_httpClientFactory.CreateClient("Rainfall"), _configuration);
        yield return new PressureChangePollingHttpClient(_httpClientFactory.CreateClient("PressureChange"), _configuration);
    }
}