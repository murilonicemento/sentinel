using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ingestion.Application.Commands;
using Ingestion.Application.DTO;
using Ingestion.Application.Events;

namespace Ingestion.IntegrationTests;

public class GetLastDetectedEventsIntegrationTests : IClassFixture<IngestionWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public GetLastDetectedEventsIntegrationTests(IngestionWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    private async Task<(Guid dataSourceId, Guid tenantId)> CreateDataSourceAsync(
        string measurementType = "Temperature",
        string collectionFrequency = "Hourly")
    {
        var command = new RegisterDataSourceCommand
        {
            Name = "Test Sensor",
            Endpoint = "http://test.api/sensor",
            DataSourceType = "Sensor",
            MeasurementType = measurementType,
            CollectionFrequency = collectionFrequency
        };

        var response = await _client.PostAsJsonAsync("/api/ingestion/data-source", command);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<ResponseBaseDTO<RegisterDatasourceResponseDTO>>(content, _jsonOptions);
        
        return (responseData!.Data.DataSourceId, responseData.Data.TenantId);
    }

    private async Task CreateSensorCollectionAsync(Guid dataSourceId, Guid tenantId, DateTime collectedAt, double sensorValue)
    {
        var command = new RegisterSensorCollectionCommand
        {
            DatasourceId = dataSourceId,
            TenantId = tenantId,
            CollectedAt = collectedAt,
            Payload = $"{{\"sensor\": \"data\", \"value\": {sensorValue}}}",
            SampleSensors = new[]
            {
                new SampleSensorDTO
                {
                    SensorValue = sensorValue,
                    Unit = "C",
                    Latitude = -23.5505,
                    Longitude = -46.6333,
                    RecordedAt = collectedAt
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/ingestion/sensor-collection", command);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetLastDetectedEvents_WithDefaultLimit_ReturnsOk()
    {
        var (dataSourceId, tenantId) = await CreateDataSourceAsync();
        var futureDate = DateTime.UtcNow.AddHours(1);
        
        await CreateSensorCollectionAsync(dataSourceId, tenantId, futureDate, 25.5);

        var response = await _client.GetAsync("/api/ingestion/last-detected-events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<ResponseBaseDTO<IEnumerable<SensorEventDetected>>>(content, _jsonOptions);
        
        Assert.NotNull(responseData);
        Assert.True(responseData.Success);
        Assert.NotNull(responseData.Data);
    }

    [Fact]
    public async Task GetLastDetectedEvents_WithCustomLimit_ReturnsOk()
    {
        var (dataSourceId, tenantId) = await CreateDataSourceAsync();
        var futureDate = DateTime.UtcNow.AddHours(1);
        
        await CreateSensorCollectionAsync(dataSourceId, tenantId, futureDate, 25.5);
        await CreateSensorCollectionAsync(dataSourceId, tenantId, futureDate.AddMinutes(10), 26.0);

        var response = await _client.GetAsync("/api/ingestion/last-detected-events?limit=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<ResponseBaseDTO<IEnumerable<SensorEventDetected>>>(content, _jsonOptions);
        
        Assert.NotNull(responseData);
        Assert.True(responseData.Success);
        Assert.NotNull(responseData.Data);
    }

    [Fact]
    public async Task GetLastDetectedEvents_WithNoEvents_ReturnsOkWithEmptyList()
    {
        var response = await _client.GetAsync("/api/ingestion/last-detected-events?limit=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<ResponseBaseDTO<IEnumerable<SensorEventDetected>>>(content, _jsonOptions);
        
        Assert.NotNull(responseData);
        Assert.True(responseData.Success);
        Assert.NotNull(responseData.Data);
        Assert.Empty(responseData.Data);
    }

    [Fact]
    public async Task GetLastDetectedEvents_WithLargeLimit_ReturnsOk()
    {
        var (dataSourceId, tenantId) = await CreateDataSourceAsync();
        var futureDate = DateTime.UtcNow.AddHours(1);
        
        await CreateSensorCollectionAsync(dataSourceId, tenantId, futureDate, 25.5);

        var response = await _client.GetAsync("/api/ingestion/last-detected-events?limit=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<ResponseBaseDTO<IEnumerable<SensorEventDetected>>>(content, _jsonOptions);
        
        Assert.NotNull(responseData);
        Assert.True(responseData.Success);
        Assert.NotNull(responseData.Data);
    }
}

