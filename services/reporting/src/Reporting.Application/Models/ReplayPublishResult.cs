namespace Reporting.Application.Models;

public sealed record ReplayPublishResult(
    string Topic,
    int Partition,
    long Offset,
    string SourceTopic,
    string RequestedBy);
