using System.Text.Json;
using Geospatial.Application.DTOs;

namespace Geospatial.Application.Mappers;

public static class BatchRequestMapper
{
    public static List<(string Type, object DomainPayload)> ToDomain(BatchRequestDTO dto)
    {
        var result = new List<(string, object)>();

        foreach (var eval in dto.Evaluations)
        {
            switch (eval.Type.ToLower())
            {
                case "contains-point":
                    var cpPayload =
                        JsonSerializer.Deserialize<ContainsPointPayloadDTO>(eval.Payload.ToString() ?? string.Empty);
                    var area = GeoPolygonMapper.ToDomain(cpPayload.Area);
                    var point = GeoPointMapper.ToDomain(cpPayload.Point);
                    result.Add((eval.Type, (area, point)));
                    break;
                case "within-radius":
                    var wrPayload =
                        JsonSerializer.Deserialize<WithinRadiusPayloadDTO>(eval.Payload.ToString() ?? string.Empty);
                    var center = GeoPointMapper.ToDomain(wrPayload.Center);
                    var target = GeoPointMapper.ToDomain(wrPayload.Point);
                    var radius = GeoRadiusMapper.ToDomain(wrPayload.Radius);
                    result.Add((eval.Type, (center, target, radius)));
                    break;
                default:
                    throw new ArgumentException($"Evaluation type '{eval.Type}' is not supported");
            }
        }

        return result;
    }
}