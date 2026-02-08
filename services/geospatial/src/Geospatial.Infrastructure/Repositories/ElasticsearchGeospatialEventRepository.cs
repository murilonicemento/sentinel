using System.Text.Json;
using Geospatial.Application.Interfaces.Repositories;
using Geospatial.Application.Models;
using Geospatial.Infrastructure.Elasticsearch;
using Microsoft.Extensions.Logging;
using Nest;
using GeoLocation = Geospatial.Application.Models.GeoLocation;

namespace Geospatial.Infrastructure.Repositories;

public class ElasticsearchGeospatialEventRepository : IGeospatialEventRepository
{
    private const string IndexName = "geospatial-events";
    private readonly IElasticClient _client;
    private readonly ILogger<ElasticsearchGeospatialEventRepository> _logger;

    public ElasticsearchGeospatialEventRepository(
        IElasticClient client,
        ILogger<ElasticsearchGeospatialEventRepository> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task IndexAsync(GeospatialOperationEvent evt, CancellationToken cancellationToken = default)
    {
        var doc = MapToDocument(evt);
        var response = await _client.IndexAsync(
            doc,
            i => i.Index(IndexName).Id(evt.EventId),
            cancellationToken);

        if (!response.IsValid)
        {
            _logger.LogError(response.OriginalException, "Failed to index geospatial event {EventId}", evt.EventId);
            throw new InvalidOperationException($"Elasticsearch index failed: {response.OriginalException?.Message}");
        }
    }

    public async Task<IReadOnlyList<GeospatialOperationEvent>> SearchByPointAsync(
        double lat,
        double lon,
        double radiusMeters,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.SearchAsync<GeospatialEventDocument>(
            s => s.Index(IndexName)
                .Query(q => q.GeoDistance(g => g
                    .Field(f => f.MainPoint)
                    .Location(new Nest.GeoLocation(lat, lon))
                    .Distance(radiusMeters, Nest.DistanceUnit.Meters))),
            cancellationToken);

        return MapFromDocuments(response.Documents);
    }

    public async Task<IReadOnlyList<GeospatialOperationEvent>> SearchByPolygonAsync(
        IReadOnlyList<(double Lat, double Lon)> polygon,
        CancellationToken cancellationToken = default)
    {
        var coordinates = polygon.Select(p => new Nest.GeoCoordinate(p.Lat, p.Lon)).ToArray();

        var response = await _client.SearchAsync<GeospatialEventDocument>(
            s => s.Index(IndexName)
                .Query(q => q.GeoPolygon(g => g
                    .Field(f => f.MainPoint)
                    .Points(coordinates))),
            cancellationToken);

        return MapFromDocuments(response.Documents);
    }

    private static GeospatialEventDocument MapToDocument(GeospatialOperationEvent evt)
    {
        Nest.GeoLocation? geoLocation = evt.MainPoint != null
            ? new Nest.GeoLocation(evt.MainPoint.Lat, evt.MainPoint.Lon)
            : null;

        return new GeospatialEventDocument
        {
            EventId = evt.EventId,
            OperationType = evt.OperationType,
            Payload = evt.Payload != null ? JsonSerializer.Serialize(evt.Payload) : null,
            Result = evt.Result != null ? JsonSerializer.Serialize(evt.Result) : null,
            Timestamp = evt.Timestamp,
            MainPoint = geoLocation
        };
    }

    private static IReadOnlyList<GeospatialOperationEvent> MapFromDocuments(IReadOnlyCollection<GeospatialEventDocument> docs)
    {
        return docs.Select(d => new GeospatialOperationEvent
        {
            EventId = d.EventId,
            OperationType = d.OperationType,
            Payload = !string.IsNullOrEmpty(d.Payload) ? JsonSerializer.Deserialize<object>(d.Payload) : null,
            Result = !string.IsNullOrEmpty(d.Result) ? JsonSerializer.Deserialize<object>(d.Result) : null,
            Timestamp = d.Timestamp,
            MainPoint = d.MainPoint != null
                ? new GeoLocation { Lat = d.MainPoint.Latitude, Lon = d.MainPoint.Longitude }
                : null
        }).ToList();
    }
}
