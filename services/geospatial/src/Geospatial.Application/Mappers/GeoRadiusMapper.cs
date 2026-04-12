using Geospatial.Application.DTOs;
using Geospatial.Domain.ValueObjects;

namespace Geospatial.Application.Mappers;

public static class GeoRadiusMapper
{
    public static GeoRadius ToDomain(RadiusDTO dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new GeoRadius(dto.Value,
            Enum.Parse<DistanceUnit>(dto.Unit, ignoreCase: true));
    }
}