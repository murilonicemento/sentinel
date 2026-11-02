using Geospatial.Application.DTOs;
using Geospatial.Domain.ValueObjects;

namespace Geospatial.Application.Mappers;

public static class GeoPointMapper
{
    public static GeoPoint ToDomain(PointDTO dto)
    {
        return dto == null
            ? throw new ArgumentNullException(nameof(dto))
            : new GeoPoint(dto.Latitude, dto.Longitude);
    }
}