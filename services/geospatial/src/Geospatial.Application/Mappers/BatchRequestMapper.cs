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
                    var cpPayload = ReadPayload<ContainsPointPayloadDTO>(eval);
                    var area = GeoPolygonMapper.ToDomain(cpPayload.Area);
                    var point = GeoPointMapper.ToDomain(cpPayload.Point);
                    result.Add((eval.Type, (area, point)));
                    break;
                case "within-radius":
                    var wrPayload = ReadPayload<WithinRadiusPayloadDTO>(eval);
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

    private static readonly JsonSerializerOptions PayloadOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>A missing, null or malformed payload is a client error (400), not an unexpected failure.</summary>
    private static T ReadPayload<T>(BatchEvaluationDTO evaluation) where T : class
    {
        var json = evaluation.Payload?.ToString();
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException($"Evaluation '{evaluation.Type}' requires a payload");

        try
        {
            return JsonSerializer.Deserialize<T>(json, PayloadOptions)
                   ?? throw new ArgumentException($"Evaluation '{evaluation.Type}' requires a payload");
        }
        catch (JsonException exception)
        {
            throw new ArgumentException($"Evaluation '{evaluation.Type}' has an invalid payload", exception);
        }
    }
}
