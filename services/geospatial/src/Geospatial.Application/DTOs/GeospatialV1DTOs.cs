using Geospatial.Domain.Events;

namespace Geospatial.Application.DTOs;

public sealed record GeospatialEventDTO(
    Guid CollectionId,
    string OperationType,
    object? Payload,
    object? Result,
    DateTimeOffset Timestamp,
    PointDTO? MainPoint)
{
    public static GeospatialEventDTO From(GeospatialOperationEvent evt) => new(
        evt.CollectionId,
        evt.OperationType,
        evt.Payload,
        evt.Result,
        evt.Timestamp,
        evt.MainPoint is null ? null : new PointDTO { Latitude = evt.MainPoint.Lat, Longitude = evt.MainPoint.Lon });
}

public sealed record ContainsPointResultDTO(bool Contains);

public sealed record WithinRadiusResultDTO(bool WithinRadius, double DistanceMeters);

public sealed record IntersectsResultDTO(bool Intersects);

public sealed record DistanceResultDTO(double DistanceMeters);

public sealed record PagedResultDTO<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalItems { get; init; }
    public int TotalPages { get; init; }
}

public sealed record ResponseBaseDTO<T>
{
    public int StatusCode { get; init; }
    public bool Success { get; init; }
    public required T Data { get; init; }
}
