using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ingestion.Application.Commands;
using Ingestion.Application.DTO;

namespace Ingestion.IntegrationTests;

public class RegisterSensorCollectionIntegrationTests : IClassFixture<IngestionWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public RegisterSensorCollectionIntegrationTests(IngestionWebApplicationFactory factory)
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
        var responseData =
            JsonSerializer.Deserialize<ResponseBaseDTO<RegisterDatasourceResponseDTO>>(content, _jsonOptions);

        return (responseData!.Data.DataSourceId, responseData.Data.TenantId);
    }

    [Fact]
    public async Task RegisterSensorCollection_WithValidCommand_ReturnsCreated()
    {
        var (dataSourceId, tenantId) = await CreateDataSourceAsync();
        var futureDate = DateTime.UtcNow.AddHours(1);
        var command = new RegisterSensorCollectionCommand
        {
            DatasourceId = dataSourceId,
            TenantId = tenantId,
            CollectedAt = futureDate,
            Payload = """{"sensor": "data", "value": 25.5}""",
            SampleSensors = new[]
            {
                new SampleSensorDTO
                {
                    SensorValue = 25.5,
                    Unit = "C",
                    Latitude = -23.5505,
                    Longitude = -46.6333,
                    RecordedAt = futureDate
                }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/ingestion/sensor-collection", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task RegisterSensorCollection_WithMultipleSamples_ReturnsCreated()
    {
        var (dataSourceId, tenantId) = await CreateDataSourceAsync();
        var futureDate = DateTime.UtcNow.AddHours(1);
        var command = new RegisterSensorCollectionCommand
        {
            DatasourceId = dataSourceId,
            TenantId = tenantId,
            CollectedAt = futureDate,
            Payload = """{"sensor": "data", "values": [25.5, 26.0, 24.8]}""",
            SampleSensors = new[]
            {
                new SampleSensorDTO
                {
                    SensorValue = 25.5,
                    Unit = "C",
                    Latitude = -23.5505,
                    Longitude = -46.6333,
                    RecordedAt = futureDate
                },
                new SampleSensorDTO
                {
                    SensorValue = 26.0,
                    Unit = "C",
                    Latitude = -23.5510,
                    Longitude = -46.6340,
                    RecordedAt = futureDate.AddMinutes(5)
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/ingestion/sensor-collection", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RegisterSensorCollection_WithInvalidDataSourceId_ReturnsBadRequest()
    {
        var (_, tenantId) = await CreateDataSourceAsync();
        var futureDate = DateTime.UtcNow.AddHours(1);
        var command = new RegisterSensorCollectionCommand
        {
            DatasourceId = Guid.NewGuid(),
            TenantId = tenantId,
            CollectedAt = futureDate,
            Payload = """{"sensor": "data"}""",
            SampleSensors = new[]
            {
                new SampleSensorDTO
                {
                    SensorValue = 25.5,
                    Unit = "C",
                    Latitude = -23.5505,
                    Longitude = -46.6333,
                    RecordedAt = futureDate
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/ingestion/sensor-collection", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterSensorCollection_WithEmptyPayload_ReturnsBadRequest()
    {
        var (dataSourceId, tenantId) = await CreateDataSourceAsync();
        var futureDate = DateTime.UtcNow.AddHours(1);
        var command = new RegisterSensorCollectionCommand
        {
            DatasourceId = dataSourceId,
            TenantId = tenantId,
            CollectedAt = futureDate,
            Payload = string.Empty,
            SampleSensors = new[]
            {
                new SampleSensorDTO
                {
                    SensorValue = 25.5,
                    Unit = "C",
                    Latitude = -23.5505,
                    Longitude = -46.6333,
                    RecordedAt = futureDate
                }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/ingestion/sensor-collection", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterSensorCollection_WithInvalidUnit_ReturnsBadRequest()
    {
        var (dataSourceId, tenantId) = await CreateDataSourceAsync("Temperature");
        var futureDate = DateTime.UtcNow.AddHours(1);
        var command = new RegisterSensorCollectionCommand
        {
            DatasourceId = dataSourceId,
            TenantId = tenantId,
            CollectedAt = futureDate,
            Payload = """{"sensor": "data"}""",
            SampleSensors = new[]
            {
                new SampleSensorDTO
                {
                    SensorValue = 25.5,
                    Unit = "InvalidUnit",
                    Latitude = -23.5505,
                    Longitude = -46.6333,
                    RecordedAt = futureDate
                }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/ingestion/sensor-collection", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterSensorCollection_WithDifferentMeasurementTypes_ReturnsCreated()
    {
        var (dataSourceId, tenantId) = await CreateDataSourceAsync("Humidity");
        var futureDate = DateTime.UtcNow.AddHours(1);
        var command = new RegisterSensorCollectionCommand
        {
            DatasourceId = dataSourceId,
            TenantId = tenantId,
            CollectedAt = futureDate,
            Payload = """{"sensor": "humidity", "value": 65.0}""",
            SampleSensors = new[]
            {
                new SampleSensorDTO
                {
                    SensorValue = 65.0,
                    Unit = "%",
                    Latitude = -23.5505,
                    Longitude = -46.6333,
                    RecordedAt = futureDate
                }
            }
        };
        var response = await _client.PostAsJsonAsync("/api/ingestion/sensor-collection", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}

