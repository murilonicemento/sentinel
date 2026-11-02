using AuditCompliance.Domain.Enums;

namespace AuditCompliance.Application.Interfaces;

public interface IAuditMetrics
{
    void EventReceived(string topic, TimeSpan? messageAge);
    void EventPersisted(string eventType, TimeSpan ingestionLag);
    void EventDuplicated(string eventType);
    void EventRejected(string reasonCode);
    void EventDeadLettered(string reasonCode);
    void ProcessingFailed(string stage);
    void FieldsRedacted(int count);
    void ExportCompleted(ExportFormatEnum format, TimeSpan duration, long recordCount);
    void ExportFailed(string reasonCode);
}
