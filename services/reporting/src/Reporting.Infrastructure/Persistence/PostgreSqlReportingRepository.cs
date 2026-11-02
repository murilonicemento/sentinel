using Microsoft.Extensions.Configuration;
using Npgsql;
using Reporting.Domain.Entities;
using Reporting.Domain.Interfaces;

namespace Reporting.Infrastructure.Persistence;

public sealed class PostgreSqlReportingRepository : IReportingRepository
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly FileReportingRepository _fallbackRepository;
    private bool _useFallback;

    public PostgreSqlReportingRepository(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _connectionString = configuration.GetConnectionString("Reporting")
            ?? configuration["ConnectionStrings:Reporting"]
            ?? "Host=localhost;Port=5432;Database=reporting;Username=postgres;Password=postgres";
        _tableName = configuration["Reporting:TableName"] ?? "reporting_events";
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
            await EnsureSchemaAsync(cancellationToken);

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand($@"
                INSERT INTO {_tableName} (event_id, event_type, tenant_id, region, severity, channel, risk_score, status, timestamp)
                VALUES (@eventId, @eventType, @tenantId, @region, @severity, @channel, @riskScore, @status, @timestamp)
                ON CONFLICT (event_id) DO NOTHING;", connection);

            command.Parameters.AddWithValue("eventId", reportingEvent.EventId);
            command.Parameters.AddWithValue("eventType", reportingEvent.EventType);
            command.Parameters.AddWithValue("tenantId", reportingEvent.TenantId);
            command.Parameters.AddWithValue("region", reportingEvent.Region);
            command.Parameters.AddWithValue("severity", reportingEvent.Severity);
            command.Parameters.AddWithValue("channel", reportingEvent.Channel);
            command.Parameters.AddWithValue("riskScore", reportingEvent.RiskScore);
            command.Parameters.AddWithValue("status", reportingEvent.Status);
            command.Parameters.AddWithValue("timestamp", reportingEvent.Timestamp);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (NpgsqlException)
        {
            _useFallback = true;
            await _fallbackRepository.ProcessEventAsync(reportingEvent, cancellationToken);
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
            await EnsureSchemaAsync(cancellationToken);

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand($@"
                SELECT event_id, event_type, tenant_id, region, severity, channel, risk_score, status, timestamp
                FROM {_tableName}
                WHERE tenant_id = @tenantId
                ORDER BY timestamp ASC;", connection);

            command.Parameters.AddWithValue("tenantId", tenantId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var events = new List<ReportingEvent>();

            while (await reader.ReadAsync(cancellationToken))
            {
                events.Add(new ReportingEvent(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetDouble(6),
                    reader.GetString(7),
                    reader.GetDateTime(8)));
            }

            return events;
        }
        catch (NpgsqlException)
        {
            _useFallback = true;
            return await _fallbackRepository.GetEventsForTenantAsync(tenantId, cancellationToken);
        }
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand($@"
            CREATE TABLE IF NOT EXISTS {_tableName} (
                event_id TEXT PRIMARY KEY,
                event_type TEXT NOT NULL,
                tenant_id TEXT NOT NULL,
                region TEXT NOT NULL,
                severity TEXT NOT NULL,
                channel TEXT NOT NULL,
                risk_score DOUBLE PRECISION NOT NULL,
                status TEXT NOT NULL,
                timestamp TIMESTAMPTZ NOT NULL
            );", connection);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
