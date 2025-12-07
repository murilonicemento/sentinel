using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ingestion.Application.Commands;
using Ingestion.Application.DTO;

namespace Ingestion.IntegrationTests;

public class RegisterDataSourceIntegrationTests : IClassFixture<IngestionWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public RegisterDataSourceIntegrationTests(IngestionWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    [Fact]
    public async Task RegisterDataSource_WithValidCommand_ReturnsCreated()
    {
        var command = new RegisterDataSourceCommand
        {
            Name = "Temperature Sensor",
            Endpoint = "http://sensor.api/temperature",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        var response = await _client.PostAsJsonAsync("/api/ingestion/data-source", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<ResponseBaseDTO<RegisterDatasourceResponseDTO>>(content, _jsonOptions);
        
        Assert.NotNull(responseData);
        Assert.True(responseData.Success);
        Assert.NotNull(responseData.Data);
        Assert.NotEqual(Guid.Empty, responseData.Data.DataSourceId);
        Assert.NotEqual(Guid.Empty, responseData.Data.TenantId);
    }

    [Theory]
    [InlineData("Sensor", "Temperature", "Hourly")]
    [InlineData("Api", "Humidity", "Daily")]
    [InlineData("File", "WindSpeed", "Weekly")]
    [InlineData("ExternalSystem", "Rainfall", "Hourly")]
    public async Task RegisterDataSource_WithAllDataSourceTypes_ReturnsCreated(
        string dataSourceType, 
        string measurementType, 
        string collectionFrequency)
    {
        var command = new RegisterDataSourceCommand
        {
            Name = $"Test {dataSourceType}",
            Endpoint = "http://test.api/endpoint",
            DataSourceType = dataSourceType,
            MeasurementType = measurementType,
            CollectionFrequency = collectionFrequency
        };

        var response = await _client.PostAsJsonAsync("/api/ingestion/data-source", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        var responseData = JsonSerializer.Deserialize<ResponseBaseDTO<RegisterDatasourceResponseDTO>>(content, _jsonOptions);
        
        Assert.NotNull(responseData);
        Assert.True(responseData.Success);
    }

    [Fact]
    public async Task RegisterDataSource_WithMissingName_ReturnsBadRequest()
    {
        var command = new RegisterDataSourceCommand
        {
            Name = string.Empty,
            Endpoint = "http://sensor.api/temperature",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };
        var response = await _client.PostAsJsonAsync("/api/ingestion/data-source", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterDataSource_WithInvalidDataSourceType_ReturnsBadRequest()
    {
        var command = new RegisterDataSourceCommand
        {
            Name = "Test Sensor",
            Endpoint = "http://sensor.api/temperature",
            DataSourceType = "InvalidType",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };
        var response = await _client.PostAsJsonAsync("/api/ingestion/data-source", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

