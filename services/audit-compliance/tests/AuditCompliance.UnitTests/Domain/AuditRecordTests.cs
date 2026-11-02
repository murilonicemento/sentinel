using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Catalog;
using AuditCompliance.Domain.Exceptions;
using AuditCompliance.Domain.ValueObjects;
using AuditCompliance.UnitTests.Support;

namespace AuditCompliance.UnitTests.Domain;

public class AuditRecordTests
{
    [Fact]
    public void Create_WithValidData_NormalizesTimestampsToUtcMilliseconds()
    {
        var occurredAt = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.FromHours(-3)).AddTicks(12345);

        var record = AuditRecord.Create("evt-1", AuditEventCatalog.RiskUpdated, 1, "risk-evaluation", occurredAt,
            TestData.Now);

        Assert.NotEqual(Guid.Empty, record.Id);
        Assert.Equal(TimeSpan.Zero, record.OccurredAt.Offset);
        Assert.Equal(new DateTimeOffset(2026, 3, 10, 12, 0, 0, 1, TimeSpan.Zero), record.OccurredAt);
        Assert.Equal(AuditRecord.CurrentSchemaVersion, record.SchemaVersion);
        Assert.Null(record.IntegrityHash);
    }

    [Theory]
    [InlineData(null, "TenantCreated", 1, "tenants-billing", AuditErrorCodes.InvalidEventId)]
    [InlineData(" ", "TenantCreated", 1, "tenants-billing", AuditErrorCodes.InvalidEventId)]
    [InlineData("evt", "SomethingElse", 1, "tenants-billing", AuditErrorCodes.UnknownEventType)]
    [InlineData("evt", "TenantCreated", 2, "tenants-billing", AuditErrorCodes.UnsupportedEventVersion)]
    [InlineData("evt", "TenantCreated", 0, "tenants-billing", AuditErrorCodes.UnsupportedEventVersion)]
    [InlineData("evt", "TenantCreated", 1, "Tenants Billing", AuditErrorCodes.InvalidSourceService)]
    [InlineData("evt", "TenantCreated", 1, null, AuditErrorCodes.InvalidSourceService)]
    public void Create_WithInvalidEnvelopeData_ThrowsWithStableCode(string? eventId, string eventType, int version,
        string? sourceService, string expectedCode)
    {
        var exception = Assert.Throws<AuditDomainException>(() =>
            AuditRecord.Create(eventId, eventType, version, sourceService, TestData.Now, TestData.Now));

        Assert.Equal(expectedCode, exception.Code);
    }

    [Fact]
    public void Create_WithEventIdLongerThanLimit_Throws()
    {
        var eventId = new string('x', AuditRecord.MaxIdentifierLength + 1);

        var exception = Assert.Throws<AuditDomainException>(() =>
            AuditRecord.Create(eventId, AuditEventCatalog.TenantCreated, 1, "tenants-billing", TestData.Now,
                TestData.Now));

        Assert.Equal(AuditErrorCodes.InvalidEventId, exception.Code);
    }

    [Fact]
    public void Create_WithOccurrenceBeyondAllowedClockSkew_Throws()
    {
        var exception = Assert.Throws<AuditDomainException>(() =>
            AuditRecord.Create("evt", AuditEventCatalog.TenantCreated, 1, "tenants-billing",
                TestData.Now.Add(AuditRecord.AllowedClockSkew).AddSeconds(1), TestData.Now));

        Assert.Equal(AuditErrorCodes.InvalidTimestamp, exception.Code);
    }

    [Fact]
    public void Create_WithOccurrenceWithinClockSkew_Succeeds()
    {
        var record = AuditRecord.Create("evt", AuditEventCatalog.TenantCreated, 1, "tenants-billing",
            TestData.Now.AddMinutes(4), TestData.Now);

        Assert.True(record.OccurredAt > record.RecordedAt);
    }

    [Fact]
    public void Create_WithEmptyTenantId_Throws()
    {
        var exception = Assert.Throws<AuditDomainException>(() =>
            AuditRecord.Create("evt", AuditEventCatalog.TenantCreated, 1, "tenants-billing", TestData.Now,
                TestData.Now, Guid.Empty));

        Assert.Equal(AuditErrorCodes.InvalidField, exception.Code);
    }

    [Fact]
    public void SealIntegrity_WhenAlreadySealed_Throws()
    {
        var record = TestData.Record();
        record.SealIntegrity("sha256:abc");

        var exception = Assert.Throws<AuditDomainException>(() => record.SealIntegrity("sha256:def"));

        Assert.Equal(AuditErrorCodes.IntegrityAlreadySealed, exception.Code);
        Assert.Equal("sha256:abc", record.IntegrityHash);
    }

    [Fact]
    public void AuditPayload_AboveMaximumSize_Throws()
    {
        var json = $"{{\"value\":\"{new string('a', AuditPayload.MaxSizeInBytes)}\"}}";

        var exception = Assert.Throws<AuditDomainException>(() => AuditPayload.FromJson(json));

        Assert.Equal(AuditErrorCodes.PayloadLimitExceeded, exception.Code);
    }

    [Fact]
    public void Filter_WithFromNotBeforeTo_Throws()
    {
        var exception = Assert.Throws<AuditDomainException>(() =>
            AuditRecordFilter.Create(TestData.TenantId, TestData.Now, TestData.Now));

        Assert.Equal(AuditErrorCodes.InvalidFilter, exception.Code);
    }
}
