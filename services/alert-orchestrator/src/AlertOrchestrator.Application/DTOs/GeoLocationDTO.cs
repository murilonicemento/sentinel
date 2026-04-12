namespace AlertOrchestrator.Application.DTOs;

public sealed record GeoLocationDTO(
    double Latitude,
    double Longitude,
    double? RadiusKm = null,
    string? PolygonWkt = null
);