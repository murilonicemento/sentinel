using Geospatial.Application.DTOs;
using Geospatial.Domain.Geometry;

namespace Geospatial.Application.Mappers;

public static class GeoPolygonMapper
{
    public static GeoPolygon ToDomain(PolygonDTO dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Coordinates == null || dto.Coordinates.Count == 0)
            throw new ArgumentException("Coordinates cannot be empty", nameof(dto));

        var points = dto.Coordinates
            .Select(c => new Domain.ValueObjects.GeoPoint(c.Latitude, c.Longitude));

        return new GeoPolygon(points);
    }
}