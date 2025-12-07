using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ingestion.Application.Commands;
using Ingestion.Application.DTO;

namespace Ingestion.IntegrationTests;

public class GetCollectionStatisticsIntegrationTests : IClassFixture<IngestionWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public GetCollectionStatisticsIntegrationTests(IngestionWebApplicationFactory factory)
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
    public async Task GetCollectionStatistics_WithValidDateRange_ReturnsOk()
    {
        var (dataSourceId, tenantId) = await CreateDataSourceAsync();
        var initialDate = DateTime.UtcNow.AddHours(-2);
        var endDate = DateTime.UtcNow.AddHours(2);
        
        await CreateSensorCollectionAsync(dataSourceId, tenantId, DateTime.UtcNow.AddHours(-1), 25.5);
        await CreateSensorCollectionAsync(dataSourceId, tenantId, DateTime.UtcNow, 26.0);

        var response = await _client.GetAsync(
            $"/api/ingestion/collection-statistics?initialDate={initialDate:O}&endDate={endDate:O}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<ResponseBaseDTO<CollectionStatisticsResponseDTO>>(content, _jsonOptions);
        
        Assert.NotNull(responseData);
        Assert.True(responseData.Success);
        Assert.NotNull(responseData.Data);
    }

    [Fact]
    public async Task GetCollectionStatistics_WithNoData_ReturnsOkWithEmptyStatistics()
    {
        var initialDate = DateTime.UtcNow.AddHours(-24);
        var endDate = DateTime.UtcNow;
        var response = await _client.GetAsync(
            $"/api/ingestion/collection-statistics?initialDate={initialDate:O}&endDate={endDate:O}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<ResponseBaseDTO<CollectionStatisticsResponseDTO>>(content, _jsonOptions);
        
        Assert.NotNull(responseData);
        Assert.True(responseData.Success);
        Assert.NotNull(responseData.Data);
        Assert.Equal(0, responseData.Data.TotalEvents);
    }

    [Fact]
    public async Task GetCollectionStatistics_WithoutDateRange_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/ingestion/collection-statistics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<ResponseBaseDTO<CollectionStatisticsResponseDTO>>(content, _jsonOptions);
        
        Assert.NotNull(responseData);
        Assert.True(responseData.Success);
        Assert.NotNull(responseData.Data);
    }

    [Fact]
    public async Task GetCollectionStatistics_WithOnlyInitialDate_ReturnsOk()
    {
        var initialDate = DateTime.UtcNow.AddHours(-24);
        var response = await _client.GetAsync($"/api/ingestion/collection-statistics?initialDate={initialDate:O}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<ResponseBaseDTO<CollectionStatisticsResponseDTO>>(content, _jsonOptions);
        
        Assert.NotNull(responseData);
        Assert.True(responseData.Success);
    }
}

