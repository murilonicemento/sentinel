using System.Drawing;
using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Ingestion.Application.Services;

public class GeospatialValidationService : IGeospatialValidationService
{
    private readonly IGeospatialClient _geospatialClient;
    private readonly ILogger<GeospatialValidationService> _logger;

    // Coordenadas válidas para o Brasil (aproximadamente)
    private static readonly PolygonDTO BrazilBounds = new()
    {
        Coordinates = new List<PointDTO>
        {
            new() { Latitude = -33.75, Longitude = -73.98 }, // Sul-Oeste
            new() { Latitude = -33.75, Longitude = -28.84 }, // Sul-Leste
            new() { Latitude = 5.27, Longitude = -28.84 }, // Norte-Leste
            new() { Latitude = 5.27, Longitude = -73.98 }, // Norte-Oeste
            new() { Latitude = -33.75, Longitude = -73.98 } // Fechar polígono
        }
    };

    public GeospatialValidationService(
        IGeospatialClient geospatialClient,
        ILogger<GeospatialValidationService> logger)
    {
        _geospatialClient = geospatialClient;
        _logger = logger;
    }

    public async Task<bool> ValidateCoordinatesAsync(double latitude, double longitude,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validação básica de range
            if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
            {
                _logger.LogWarning(
                    "Invalid coordinate range. Latitude: {Latitude}, Longitude: {Longitude}",
                    latitude,
                    longitude);
                return false;
            }

            var point = new PointDTO { Latitude = latitude, Longitude = longitude };

            // Valida se o ponto está dentro dos limites do Brasil
            // Em produção, isso pode ser configurável por tenant ou região
            var isValid = await _geospatialClient.ContainsPointAsync(point, BrazilBounds, cancellationToken);

            if (!isValid)
            {
                _logger.LogWarning(
                    "Coordinates outside valid area. Latitude: {Latitude}, Longitude: {Longitude}",
                    latitude,
                    longitude);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error validating coordinates. Latitude: {Latitude}, Longitude: {Longitude}",
                latitude,
                longitude);
            // Em caso de erro no serviço geoespacial, aceita as coordenadas para não bloquear o fluxo
            // Em produção, pode ser configurável se deve falhar ou aceitar
            return true;
        }
    }
}