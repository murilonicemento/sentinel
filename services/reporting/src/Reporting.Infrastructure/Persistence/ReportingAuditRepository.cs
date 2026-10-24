using Microsoft.Extensions.Configuration;
using Npgsql;
using Reporting.Domain.Entities;
using Reporting.Domain.Interfaces;

namespace Reporting.Infrastructure.Persistence;

public sealed class ReportingAuditRepository : IReportingAuditRepository
{
    private readonly string _connectionString;
    private readonly string _tableName;

    public ReportingAuditRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Reporting")
            ?? configuration["ConnectionStrings:Reporting"]
            ?? "Host=localhost;Port=5432;Database=reporting;Username=postgres;Password=postgres";

        _tableName = configuration["Reporting:AuditTableName"] ?? "reporting_event_audits";
    }

    public async Task SaveAsync(ReportingEventProcessingAudit audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audit);

        await EnsureSchemaAsync(cancellationToken);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand($@"
                INSERT INTO {_tableName} (event_id, source_topic, partition, offset, status, attempt_count, error_message, processed_at)
                VALUES (@eventId, @sourceTopic, @partition, @offset, @status, @attemptCount, @errorMessage, @processedAt)", connection);

        command.Parameters.AddWithValue("eventId", audit.EventId);
        command.Parameters.AddWithValue("sourceTopic", audit.SourceTopic);
        command.Parameters.AddWithValue("partition", audit.Partition);
        command.Parameters.AddWithValue("offset", audit.Offset);
        command.Parameters.AddWithValue("status", audit.Status);
        command.Parameters.AddWithValue("attemptCount", audit.AttemptCount);
        command.Parameters.AddWithValue("errorMessage", (object?)audit.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("processedAt", audit.ProcessedAt);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand($@"
            CREATE TABLE IF NOT EXISTS {_tableName} (
                event_id TEXT NOT NULL,
                source_topic TEXT NOT NULL,
                partition INTEGER NOT NULL,
                offset BIGINT NOT NULL,
                status TEXT NOT NULL,
                attempt_count INTEGER NOT NULL,
                error_message TEXT,
                processed_at TIMESTAMPTZ NOT NULL
            );", connection);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
