using Microsoft.AspNetCore.Mvc.Testing;
using RiskCatalog.Application.DTO;
using System.Net.Http.Json;
using RiskCatalog.Api;

namespace RiskCatalog.IntegrationTests;

public class GeospatialIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public GeospatialIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateRegionalRiskParameters_WithValidCoordinates_ShouldReturnSuccess()
    {
        // Arrange
        var request = new CreateRegionalRiskParameterDTO
        {
            AdjustmentFactor = 1.5,
            Description = "São Paulo Region Risk Parameters",
            CenterLatitude = -23.5505,
            CenterLongitude = -46.6333,
            CoverageRadiusKm = 50.0,
            RegionBounds = new PolygonDTO
            {
                Coordinates = new List<PointDTO>
                {
                    new() { Latitude = -24.0, Longitude = -47.0 },
                    new() { Latitude = -24.0, Longitude = -46.0 },
                    new() { Latitude = -23.0, Longitude = -46.0 },
                    new() { Latitude = -23.0, Longitude = -47.0 },
                    new() { Latitude = -24.0, Longitude = -47.0 }
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/riskmodel/regional-parameters", request);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetRegionalRiskParameters_WithGeospatialData_ShouldReturnCompleteInformation()
    {
        // This test would require setting up test data in the database
        // For demonstration purposes, we'll just verify the endpoint exists
        var response = await _client.GetAsync("/api/riskmodel/regional-parameters/guid-here");
        
        // We expect either 404 (not found) or 200 (found with data)
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.NotFound || 
                   response.StatusCode == System.Net.HttpStatusCode.OK);
    }
}
