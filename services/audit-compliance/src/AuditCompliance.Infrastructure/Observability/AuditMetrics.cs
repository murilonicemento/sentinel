using System.Diagnostics;
using System.Diagnostics.Metrics;
using AuditCompliance.Application.Interfaces;
using AuditCompliance.Domain.Enums;

namespace AuditCompliance.Infrastructure.Observability;

/// <summary>
/// Os rótulos usam somente valores de baixa cardinalidade (tópico, tipo de evento do catálogo,
/// códigos de erro e formato). Tenant, ator e serviço de origem não são usados como rótulo.
/// </summary>
public sealed class AuditMetrics : IAuditMetrics
{
    public const string MeterName = "AuditCompliance";

    private readonly Counter<long> _deadLettered;
    private readonly Counter<long> _duplicated;
    private readonly Histogram<double> _exportDuration;
    private readonly Counter<long> _exportFailed;
    private readonly Counter<long> _exportRecords;
    private readonly Histogram<double> _ingestionLag;
    private readonly Histogram<double> _messageAge;
    private readonly Counter<long> _persisted;
    private readonly Counter<long> _processingFailures;
    private readonly Counter<long> _received;
    private readonly Counter<long> _redacted;
    private readonly Counter<long> _rejected;

    public AuditMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName, "1.0.0");

        _received = meter.CreateCounter<long>("audit_events_received", description: "Audit messages received");
        _persisted = meter.CreateCounter<long>("audit_events_persisted", description: "Audit records persisted");
        _duplicated = meter.CreateCounter<long>("audit_events_duplicated",
            description: "Duplicate audit events ignored by the uniqueness constraint");
        _rejected = meter.CreateCounter<long>("audit_events_rejected",
            description: "Audit events rejected by contract validation");
        _deadLettered = meter.CreateCounter<long>("audit_events_dead_lettered",
            description: "Audit messages published to the dead-letter topic");
        _processingFailures = meter.CreateCounter<long>("audit_processing_failures",
            description: "Transient processing failures (persistence or dead-letter publication)");
        _redacted = meter.CreateCounter<long>("audit_fields_redacted",
            description: "Sensitive fields redacted from metadata and states");
        _ingestionLag = meter.CreateHistogram<double>("audit_ingestion_lag_seconds", "s",
            "Time between the occurrence at the source and persistence");
        _messageAge = meter.CreateHistogram<double>("audit_message_age_seconds", "s",
            "Time between broker publication and consumption");
        _exportDuration = meter.CreateHistogram<double>("audit_export_duration_seconds", "s",
            "Export generation duration");
        _exportRecords = meter.CreateCounter<long>("audit_export_records", description: "Records written to exports");
        _exportFailed = meter.CreateCounter<long>("audit_exports_failed", description: "Failed export attempts");
    }

    public void EventReceived(string topic, TimeSpan? messageAge)
    {
        _received.Add(1, new KeyValuePair<string, object?>("topic", topic));

        if (messageAge is not null && messageAge.Value >= TimeSpan.Zero)
            _messageAge.Record(messageAge.Value.TotalSeconds, new KeyValuePair<string, object?>("topic", topic));
    }

    public void EventPersisted(string eventType, TimeSpan ingestionLag)
    {
        var tag = new KeyValuePair<string, object?>("event_type", eventType);
        _persisted.Add(1, tag);
        _ingestionLag.Record(Math.Max(0, ingestionLag.TotalSeconds), tag);
    }

    public void EventDuplicated(string eventType)
    {
        _duplicated.Add(1, new KeyValuePair<string, object?>("event_type", eventType));
    }

    public void EventRejected(string reasonCode)
    {
        _rejected.Add(1, new KeyValuePair<string, object?>("reason", reasonCode));
    }

    public void EventDeadLettered(string reasonCode)
    {
        _deadLettered.Add(1, new KeyValuePair<string, object?>("reason", reasonCode));
    }

    public void ProcessingFailed(string stage)
    {
        _processingFailures.Add(1, new KeyValuePair<string, object?>("stage", stage));
    }

    public void FieldsRedacted(int count)
    {
        _redacted.Add(count);
    }

    public void ExportCompleted(ExportFormatEnum format, TimeSpan duration, long recordCount)
    {
        var tag = new KeyValuePair<string, object?>("format", format.ToString());
        _exportDuration.Record(duration.TotalSeconds, tag);
        _exportRecords.Add(recordCount, tag);
    }

    public void ExportFailed(string reasonCode)
    {
        _exportFailed.Add(1, new KeyValuePair<string, object?>("reason", reasonCode));
    }
}

public static class AuditTelemetry
{
    public const string ActivitySourceName = "AuditCompliance";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
