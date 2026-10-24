using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Enums;
using AuditCompliance.Domain.Exceptions;
using AuditCompliance.UnitTests.Support;

namespace AuditCompliance.UnitTests.Domain;

public class AuditExportTests
{
    [Fact]
    public void Request_CreatesPendingExportBoundToFilterTenant()
    {
        var export = AuditExport.Request(TestData.Filter(), ExportFormatEnum.Json, "user-1", TestData.Now);

        Assert.Equal(ExportStatusEnum.Pending, export.Status);
        Assert.Equal(TestData.TenantId, export.TenantId);
        Assert.EndsWith(".json", export.FileName);
    }

    [Fact]
    public void Request_WithoutRequester_Throws()
    {
        Assert.Throws<AuditDomainException>(() =>
            AuditExport.Request(TestData.Filter(), ExportFormatEnum.Csv, " ", TestData.Now));
    }

    [Fact]
    public void Complete_WhenNotProcessing_Throws()
    {
        var export = AuditExport.Request(TestData.Filter(), ExportFormatEnum.Csv, "user-1", TestData.Now);

        var exception = Assert.Throws<AuditDomainException>(() =>
            export.Complete("file", 1, TestData.Now, TestData.Now.AddHours(1)));

        Assert.Equal(AuditErrorCodes.InvalidExportState, exception.Code);
    }

    [Fact]
    public void Complete_WhenProcessing_IsDownloadableUntilExpiration()
    {
        var export = TestData.Export(ExportStatusEnum.Processing);

        export.Complete("file-1", 42, TestData.Now, TestData.Now.AddHours(24));

        Assert.Equal(ExportStatusEnum.Completed, export.Status);
        Assert.True(export.IsAvailableForDownload(TestData.Now.AddHours(23)));
        Assert.False(export.IsAvailableForDownload(TestData.Now.AddHours(24)));
        Assert.True(export.HasExpired(TestData.Now.AddHours(24)));
    }

    [Fact]
    public void Fail_WhenProcessing_RecordsFailureCode()
    {
        var export = TestData.Export(ExportStatusEnum.Processing);

        export.Fail("EXPORT_LIMIT_EXCEEDED", TestData.Now);

        Assert.Equal(ExportStatusEnum.Failed, export.Status);
        Assert.Equal("EXPORT_LIMIT_EXCEEDED", export.FailureCode);
        Assert.False(export.IsAvailableForDownload(TestData.Now));
    }
}
