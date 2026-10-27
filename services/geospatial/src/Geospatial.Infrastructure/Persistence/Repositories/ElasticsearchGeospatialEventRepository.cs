using System.Text.Json;
using Geospatial.Domain.Events;
using Geospatial.Domain.Repositories;
using Geospatial.Infrastructure.Elasticsearch;
using Microsoft.Extensions.Logging;
using Nest;
using GeoLocation = Geospatial.Domain.Events.GeoLocation;

namespace Geospatial.Infrastructure.Persistence.Repositories;

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
            i => i.Index(IndexName).Id(evt.SourceEventId ?? evt.CollectionId),
            cancellationToken);

        if (!response.IsValid)
        {
            var status = response.ApiCall?.HttpStatusCode;
            // No status means the cluster could not be reached; 5xx and 429 may succeed on retry.
            if (status is null or >= 500 or 429)
                throw new ElasticsearchUnavailableException(status, response.OriginalException);

            _logger.LogError(response.OriginalException, "Elasticsearch rejected geospatial event {EventId} with {Status}",
                evt.CollectionId, status);
            throw new InvalidOperationException($"Elasticsearch rejected the document with status {status}.");
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

    public async Task<GeospatialEventPage> SearchByPointAsync(
        double lat,
        double lon,
        double radiusMeters,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var response = await _client.SearchAsync<GeospatialEventDocument>(
            s => s.Index(IndexName)
                .From(skip)
                .Size(take)
                .TrackTotalHits()
                .Sort(sort => sort.Descending(f => f.Timestamp))
                .Query(q => q.GeoDistance(g => g
                    .Field(f => f.MainPoint)
                    .Location(new Nest.GeoLocation(lat, lon))
                    .Distance(radiusMeters, Nest.DistanceUnit.Meters))),
            cancellationToken);

        return ToPage(response);
    }

    public async Task<GeospatialEventPage> SearchByPolygonAsync(
        IReadOnlyList<(double Lat, double Lon)> polygon,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var coordinates = polygon.Select(p => new Nest.GeoCoordinate(p.Lat, p.Lon)).ToArray();

        var response = await _client.SearchAsync<GeospatialEventDocument>(
            s => s.Index(IndexName)
                .From(skip)
                .Size(take)
                .TrackTotalHits()
                .Sort(sort => sort.Descending(f => f.Timestamp))
                .Query(q => q.GeoPolygon(g => g
                    .Field(f => f.MainPoint)
                    .Points(coordinates))),
            cancellationToken);

        return ToPage(response);
    }

    private GeospatialEventPage ToPage(ISearchResponse<GeospatialEventDocument> response)
    {
        if (!response.IsValid)
        {
            _logger.LogError(response.OriginalException, "Geospatial event search failed");
            throw new InvalidOperationException("Elasticsearch search failed.", response.OriginalException);
        }

        return new GeospatialEventPage(MapFromDocuments(response.Documents), response.Total);
    }

    private static GeospatialEventDocument MapToDocument(GeospatialOperationEvent evt)
    {
        Nest.GeoLocation? geoLocation = evt.MainPoint != null
            ? new Nest.GeoLocation(evt.MainPoint.Lat, evt.MainPoint.Lon)
            : null;

        return new GeospatialEventDocument
        {
            EventId = evt.CollectionId,
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
            CollectionId = d.EventId,
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

/// <summary>Elasticsearch could not be reached or answered with a retryable status.</summary>
public sealed class ElasticsearchUnavailableException(int? status, Exception? innerException)
    : Exception($"Elasticsearch is unavailable (status {status?.ToString() ?? "none"}).", innerException);
