using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Infrastructure.Integrity;
using AuditCompliance.Infrastructure.Persistence.Documents;
using static AuditCompliance.IntegrationTests.AuditRecordsApiTests;

namespace AuditCompliance.IntegrationTests;

public class Sha256IntegrityHasherTests
{
    private readonly Sha256IntegrityHasher _hasher = new();

    [Fact]
    public void Verify_AfterPersistenceRoundTrip_IsValid()
    {
        var record = NewRecord(Guid.NewGuid(), DateTimeOffset.UtcNow.AddTicks(7), withStates: true);
        record.SealIntegrity(_hasher.Compute(record));

        var roundTripped = AuditRecordDocument.FromDomain(record).ToDomain();

        Assert.True(_hasher.Verify(roundTripped));
    }

    [Fact]
    public void Compute_IsDeterministicAndSensitiveToContent()
    {
        var record = NewRecord(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var document = AuditRecordDocument.FromDomain(record);
        document.Action = "plan.cancel";

        Assert.Equal(_hasher.Compute(record), _hasher.Compute(record));
        Assert.NotEqual(_hasher.Compute(record), _hasher.Compute(document.ToDomain()));
        Assert.Matches("^sha256:[0-9a-f]{64}$", _hasher.Compute(record));
    }

    [Fact]
    public void Verify_WithoutHash_ReturnsNull()
    {
        Assert.Null(_hasher.Verify(NewRecord(Guid.NewGuid(), DateTimeOffset.UtcNow)));
    }
}
