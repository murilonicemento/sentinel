using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Configuration;
using Reporting.Domain.Entities;
using Reporting.Domain.Interfaces;
using StackExchange.Redis;

namespace Reporting.Infrastructure.Persistence;

public sealed class ElasticsearchReportingRepository : IReportingRepository
{
    private readonly ElasticsearchClient _elasticClient;
    private readonly IDatabase _cache;
    private readonly PostgreSqlReportingRepository _postgresRepository;
    private readonly string _indexName;
    private readonly FileReportingRepository _fallbackRepository;
    private bool _useFallback;

    public ElasticsearchReportingRepository(
        ElasticsearchClient elasticClient,
        IDatabase cache,
        IConfiguration configuration)
    {
        _elasticClient = elasticClient;
        _cache = cache;
        _indexName = configuration["Elasticsearch:IndexName"] ?? "reporting-events";
        _postgresRepository = new PostgreSqlReportingRepository(configuration);
        _fallbackRepository = new FileReportingRepository(Path.Combine(AppContext.BaseDirectory, "reporting-events.json"));
    }

    public async Task ProcessEventAsync(ReportingEvent reportingEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reportingEvent);

        if (string.IsNullOrWhiteSpace(reportingEvent.TenantId))
        {
            throw new ArgumentException("A tenant identifier is required.", nameof(reportingEvent));
        }

        if (_useFallback)
        {
            await _fallbackRepository.ProcessEventAsync(reportingEvent, cancellationToken);
            return;
        }

        try
        {
            await _postgresRepository.ProcessEventAsync(reportingEvent, cancellationToken);
        }
        catch (Exception)
        {
            _useFallback = true;
            await _fallbackRepository.ProcessEventAsync(reportingEvent, cancellationToken);
            return;
        }

        try
        {
            await IndexAsync(reportingEvent, cancellationToken);
        }
        catch
        {
            // Elasticsearch is best-effort. If indexing fails, continue using Postgres data.
        }

        try
        {
            await CacheAsync(reportingEvent, cancellationToken);
        }
        catch
        {
            // Redis caching is best-effort. Ignore cache failures.
        }
    }

    public async Task<IReadOnlyCollection<ReportingEvent>> GetEventsForTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Array.Empty<ReportingEvent>();
        }

        if (_useFallback)
        {
            return await _fallbackRepository.GetEventsForTenantAsync(tenantId, cancellationToken);
        }

        try
        {
            var cacheKey = $"reporting:events:{tenantId}";
            try
            {
                var cached = await _cache.StringGetAsync(cacheKey);
                if (cached.HasValue)
                {
                    return JsonSerializer.Deserialize<List<ReportingEvent>>(cached!) ?? new List<ReportingEvent>();
                }
            }
            catch
            {
                // Redis is best-effort; if cache access fails, continue without it.
            }

            try
            {
                var response = await _elasticClient.SearchAsync<ReportingEvent>(s => s
                    .Index(_indexName)
                    .Query(q => q.Term(t => t.Field("tenantId").Value(tenantId))), cancellationToken);

                if (response.IsValidResponse)
                {
                    var events = response.Documents.OrderBy(x => x.Timestamp).ToList();
                    try
                    {
                        await _cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(events, new JsonSerializerOptions { WriteIndented = false }), TimeSpan.FromMinutes(5));
                    }
                    catch
                    {
                        // Redis cache write is best-effort.
                    }

                    return events;
                }
            }
            catch
            {
                // Elasticsearch query failed, fallback to Postgres.
            }

            return await _postgresRepository.GetEventsForTenantAsync(tenantId, cancellationToken);
        }
        catch (Exception)
        {
            _useFallback = true;
            return await _fallbackRepository.GetEventsForTenantAsync(tenantId, cancellationToken);
        }
    }

    private async Task IndexAsync(ReportingEvent reportingEvent, CancellationToken cancellationToken)
    {
        var indexResponse = await _elasticClient.IndexAsync(reportingEvent, i => i.Index(_indexName).Id(reportingEvent.EventId), cancellationToken);
        if (!indexResponse.IsValidResponse)
        {
            throw new InvalidOperationException(indexResponse.DebugInformation ?? "Failed to index event in Elasticsearch.");
        }
    }

    private async Task CacheAsync(ReportingEvent reportingEvent, CancellationToken cancellationToken)
    {
        var cacheKey = $"reporting:event:{reportingEvent.TenantId}:{reportingEvent.EventId}";
        await _cache.StringSetAsync(cacheKey, JsonSerializer.Serialize(reportingEvent, CreateJsonOptions()), TimeSpan.FromMinutes(10));
    }

    private static JsonSerializerOptions CreateJsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };
}
