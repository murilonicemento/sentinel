using AuditCompliance.Application.Common;
using AuditCompliance.Application.Ingestion;
using AuditCompliance.Application.Interfaces;
using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Enums;
using AuditCompliance.Domain.Exceptions;
using AuditCompliance.Domain.Repositories;
using AuditCompliance.UnitTests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AuditCompliance.UnitTests.Application;

public class IngestAuditEventHandlerTests
{
    private const string ValidEnvelope = """
                                         {
                                           "eventId": "evt-123",
                                           "eventType": "PlanChanged",
                                           "eventVersion": 1,
                                           "sourceService": "tenants-billing",
                                           "occurredAt": "2026-03-10T11:59:00Z",
                                           "tenantId": "11111111-1111-1111-1111-111111111111",
                                           "correlationId": "corr-1",
                                           "action": "plan.change",
                                           "actor": { "type": "user", "id": "user-42" },
                                           "resource": { "type": "Tenant", "id": "11111111-1111-1111-1111-111111111111" },
                                           "metadata": { "fromPlan": "basic", "toPlan": "pro", "token": "secret-value" },
                                           "afterState": { "planId": "pro" },
                                           "unknownField": "ignored"
                                         }
                                         """;

    private readonly Mock<IIntegrityHasher> _hasher = new();
    private readonly Mock<IAuditMetrics> _metrics = new();
    private readonly Mock<IAuditRecordRepository> _repository = new();
    private readonly IngestAuditEventHandler _handler;

    public IngestAuditEventHandlerTests()
    {
        _hasher.Setup(h => h.Compute(It.IsAny<AuditRecord>())).Returns("sha256:test");
        _handler = new IngestAuditEventHandler(_repository.Object, _hasher.Object, _metrics.Object,
            new FixedTimeProvider(TestData.Now), NullLogger<IngestAuditEventHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WithValidEnvelope_PersistsSanitizedSealedRecord()
    {
        AuditRecord? persisted = null;
        _repository.Setup(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()))
            .Callback<AuditRecord, CancellationToken>((record, _) => persisted = record)
            .ReturnsAsync(AppendResultEnum.Appended);

        var outcome = await _handler.HandleAsync(Message(ValidEnvelope), CancellationToken.None);

        Assert.Equal(IngestionStatusEnum.Persisted, outcome.Status);
        Assert.NotNull(persisted);
        Assert.Equal(persisted!.Id, outcome.RecordId);
        Assert.Equal("evt-123", persisted.EventId);
        Assert.Equal(TestData.TenantId, persisted.TenantId);
        Assert.Equal(ActorTypeEnum.User, persisted.Actor!.Type);
        Assert.Equal("Tenant", persisted.Resource!.Type);
        Assert.Equal(TestData.Now, persisted.RecordedAt);
        Assert.Equal("sha256:test", persisted.IntegrityHash);
        Assert.DoesNotContain("secret-value", persisted.Metadata!.Json);
        Assert.Contains("\"toPlan\":\"pro\"", persisted.Metadata.Json);
        _metrics.Verify(m => m.EventPersisted("PlanChanged", TimeSpan.FromMinutes(1)), Times.Once);
        _metrics.Verify(m => m.FieldsRedacted(1), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryReportsDuplicate_ReturnsDuplicate()
    {
        _repository.Setup(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AppendResultEnum.Duplicate);

        var outcome = await _handler.HandleAsync(Message(ValidEnvelope), CancellationToken.None);

        Assert.Equal(IngestionStatusEnum.Duplicate, outcome.Status);
        _metrics.Verify(m => m.EventDuplicated("PlanChanged"), Times.Once);
        _metrics.Verify(m => m.EventPersisted(It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Theory]
    [InlineData("not json", ApplicationErrorCodes.MalformedMessage)]
    [InlineData("", ApplicationErrorCodes.MalformedMessage)]
    [InlineData("null", ApplicationErrorCodes.MalformedMessage)]
    [InlineData("""{"eventId":"e","eventType":"PlanChanged","sourceService":"svc-a","occurredAt":"2026-03-10T11:00:00Z"}""",
        ApplicationErrorCodes.MissingRequiredField)]
    [InlineData("""{"eventId":"e","eventType":"PlanChanged","eventVersion":1,"sourceService":"svc-a"}""",
        ApplicationErrorCodes.MissingRequiredField)]
    [InlineData("""{"eventId":"e","eventType":"PlanChanged","eventVersion":1,"sourceService":"svc-a","occurredAt":"2026-03-10T11:00:00"}""",
        AuditErrorCodes.InvalidTimestamp)]
    [InlineData("""{"eventId":"e","eventType":"Unknown","eventVersion":1,"sourceService":"svc-a","occurredAt":"2026-03-10T11:00:00Z"}""",
        AuditErrorCodes.UnknownEventType)]
    [InlineData("""{"eventId":"e","eventType":"PlanChanged","eventVersion":1,"sourceService":"svc-a","occurredAt":"2026-03-10T11:00:00Z","tenantId":"abc"}""",
        AuditErrorCodes.InvalidField)]
    [InlineData("""{"eventId":"e","eventType":"PlanChanged","eventVersion":1,"sourceService":"svc-a","occurredAt":"2026-03-10T11:00:00Z","actor":{"type":"1","id":"x"}}""",
        AuditErrorCodes.InvalidActor)]
    [InlineData("""{"eventId":"e","eventType":"PlanChanged","eventVersion":1,"sourceService":"svc-a","occurredAt":"2026-03-10T11:00:00Z","metadata":"text"}""",
        AuditErrorCodes.InvalidField)]
    public async Task HandleAsync_WithInvalidMessage_RejectsWithoutPersisting(string payload, string expectedCode)
    {
        var outcome = await _handler.HandleAsync(Message(payload), CancellationToken.None);

        Assert.Equal(IngestionStatusEnum.Rejected, outcome.Status);
        Assert.Equal(expectedCode, outcome.RejectionCode);
        _repository.Verify(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        _metrics.Verify(m => m.EventRejected(expectedCode), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryFails_PropagatesSoMessageIsNotAcknowledged()
    {
        _repository.Setup(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException());

        await Assert.ThrowsAsync<TimeoutException>(() =>
            _handler.HandleAsync(Message(ValidEnvelope), CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_RecordsMessageAgeFromBrokerTimestamp()
    {
        _repository.Setup(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AppendResultEnum.Appended);

        await _handler.HandleAsync(Message(ValidEnvelope, TestData.Now.AddSeconds(-30)), CancellationToken.None);

        _metrics.Verify(m => m.EventReceived("audit-events", TimeSpan.FromSeconds(30)), Times.Once);
    }

    private static IncomingAuditMessage Message(string payload, DateTimeOffset? brokerTimestamp = null)
    {
        return new IncomingAuditMessage(payload, "audit-events", 0, 10, brokerTimestamp);
    }
}
