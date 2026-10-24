using AuditCompliance.Application.Common;
using AuditCompliance.Application.Exports;
using AuditCompliance.Application.Exports.Formatters;
using AuditCompliance.Application.Interfaces;
using AuditCompliance.Application.Policies;
using AuditCompliance.Application.Records;
using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Catalog;
using AuditCompliance.Domain.Enums;
using AuditCompliance.Domain.Repositories;
using AuditCompliance.Domain.ValueObjects;
using AuditCompliance.UnitTests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AuditCompliance.UnitTests.Application;

public class ExportHandlersTests
{
    private readonly Mock<IAuditExportRepository> _exports = new();
    private readonly Mock<IExportFileStorage> _files = new();
    private readonly IAuditExportFormatter[] _formatters = [new CsvAuditExportFormatter(), new JsonAuditExportFormatter()];
    private readonly Mock<IIntegrityHasher> _hasher = new();
    private readonly Mock<IAuditMetrics> _metrics = new();
    private readonly Mock<IAuditRecordQueryRepository> _queries = new();
    private readonly Mock<IAuditRecordRepository> _records = new();
    private readonly FixedTimeProvider _time = new(TestData.Now);

    public ExportHandlersTests()
    {
        _hasher.Setup(h => h.Compute(It.IsAny<AuditRecord>())).Returns("sha256:test");
        _records.Setup(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AppendResultEnum.Appended);
    }

    [Fact]
    public async Task Request_WithInvalidFormat_ReturnsValidationError()
    {
        var result = await RequestHandler().HandleAsync(Command("xml"), CancellationToken.None);

        Assert.Equal(ErrorTypeEnum.Validation, result.Error!.Type);
        Assert.Contains("format", result.Error.FieldErrors!.Keys);
        _exports.Verify(e => e.AddAsync(It.IsAny<AuditExport>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Request_WhenMatchingRecordsExceedLimit_ReturnsUnprocessableWithoutCreatingExport()
    {
        _queries.Setup(q => q.CountAsync(It.IsAny<AuditRecordFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(11);

        var result = await RequestHandler(maxRecords: 10).HandleAsync(Command("csv"), CancellationToken.None);

        Assert.Equal(ApplicationErrorCodes.ExportLimitExceeded, result.Error!.Code);
        Assert.Equal(ErrorTypeEnum.Unprocessable, result.Error.Type);
        _exports.Verify(e => e.AddAsync(It.IsAny<AuditExport>(), It.IsAny<CancellationToken>()), Times.Never);
        _records.Verify(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Request_WhenValid_RecordsSelfAuditBeforeCreatingExport()
    {
        var calls = new List<string>();
        AuditRecord? selfAudit = null;
        _queries.Setup(q => q.CountAsync(It.IsAny<AuditRecordFilter>(), It.IsAny<CancellationToken>())).ReturnsAsync(3);
        _records.Setup(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()))
            .Callback<AuditRecord, CancellationToken>((record, _) =>
            {
                calls.Add("audit");
                selfAudit = record;
            })
            .ReturnsAsync(AppendResultEnum.Appended);
        _exports.Setup(e => e.AddAsync(It.IsAny<AuditExport>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("export"))
            .Returns(Task.CompletedTask);

        var result = await RequestHandler().HandleAsync(Command("JSON"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value!.Status);
        Assert.Equal("Json", result.Value.Format);
        Assert.Equal(["audit", "export"], calls);
        Assert.Equal(AuditEventCatalog.AuditExportRequested, selfAudit!.EventType);
        Assert.Equal(TestData.TenantId, selfAudit.TenantId);
        Assert.Equal("user-1", selfAudit.Actor!.Id);
        Assert.Equal(result.Value.Id.ToString(), selfAudit.Resource!.Id);
    }

    [Fact]
    public async Task Download_WhenExportNotFound_ReturnsNotFound()
    {
        var result = await DownloadHandler().HandleAsync(DownloadQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ErrorTypeEnum.NotFound, result.Error!.Type);
    }

    [Theory]
    [InlineData(ExportStatusEnum.Pending, ApplicationErrorCodes.ExportNotReady, ErrorTypeEnum.Conflict)]
    [InlineData(ExportStatusEnum.Processing, ApplicationErrorCodes.ExportNotReady, ErrorTypeEnum.Conflict)]
    [InlineData(ExportStatusEnum.Failed, ApplicationErrorCodes.ExportFailed, ErrorTypeEnum.Conflict)]
    [InlineData(ExportStatusEnum.Expired, ApplicationErrorCodes.ExportExpired, ErrorTypeEnum.Gone)]
    public async Task Download_WhenExportUnavailable_ReturnsErrorWithoutAuditOrFileAccess(ExportStatusEnum status,
        string expectedCode, ErrorTypeEnum expectedType)
    {
        var export = TestData.Export(status);
        SetupExport(export);

        var result = await DownloadHandler().HandleAsync(DownloadQuery(export.Id), CancellationToken.None);

        Assert.Equal(expectedCode, result.Error!.Code);
        Assert.Equal(expectedType, result.Error.Type);
        _files.Verify(f => f.OpenReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _records.Verify(r => r.AppendAsync(It.IsAny<AuditRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Download_WhenCompletedExportExpired_ReturnsGone()
    {
        var export = TestData.Export(ExportStatusEnum.Completed, expiresAt: TestData.Now.AddSeconds(-1), fileId: "f");
        SetupExport(export);

        var result = await DownloadHandler().HandleAsync(DownloadQuery(export.Id), CancellationToken.None);

        Assert.Equal(ApplicationErrorCodes.ExportExpired, result.Error!.Code);
    }

    [Fact]
    public async Task Download_WhenAvailable_AuditsAndReturnsFile()
    {
        var export = TestData.Export(ExportStatusEnum.Completed, expiresAt: TestData.Now.AddHours(1), fileId: "file-1");
        SetupExport(export);
        _files.Setup(f => f.OpenReadAsync("file-1", It.IsAny<CancellationToken>())).ReturnsAsync(new MemoryStream());

        var result = await DownloadHandler().HandleAsync(DownloadQuery(export.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("text/csv", result.Value!.ContentType);
        Assert.Equal(export.FileName, result.Value.FileName);
        _records.Verify(r => r.AppendAsync(
            It.Is<AuditRecord>(a => a.EventType == AuditEventCatalog.AuditExportDownloaded && a.Actor!.Id == "user-2"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_WhenNothingToClaim_ReturnsFalse()
    {
        Assert.False(await ProcessHandler().ProcessNextAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Process_WhenRecordsExceedLimit_AbortsUploadAndFailsExport()
    {
        var export = TestData.Export(ExportStatusEnum.Processing);
        var upload = SetupClaimAndUpload(export, [TestData.Record(eventId: "a"), TestData.Record(eventId: "b")]);

        Assert.True(await ProcessHandler(maxRecords: 1).ProcessNextAsync(CancellationToken.None));

        upload.Verify(u => u.AbortAsync(It.IsAny<CancellationToken>()), Times.Once);
        upload.Verify(u => u.CompleteAsync(It.IsAny<CancellationToken>()), Times.Never);
        _exports.Verify(e => e.SaveProcessingResultAsync(
            It.Is<AuditExport>(x => x.Status == ExportStatusEnum.Failed && x.FailureCode == ApplicationErrorCodes.ExportLimitExceeded),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_WhenSuccessful_CompletesExportWithExpiration()
    {
        var export = TestData.Export(ExportStatusEnum.Processing);
        var upload = SetupClaimAndUpload(export, [TestData.Record(eventId: "a"), TestData.Record(eventId: "b")]);
        _exports.Setup(e => e.SaveProcessingResultAsync(export, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await ProcessHandler().ProcessNextAsync(CancellationToken.None);

        Assert.Equal(ExportStatusEnum.Completed, export.Status);
        Assert.Equal(2, export.RecordCount);
        Assert.Equal("file-1", export.FileId);
        Assert.Equal(TestData.Now.AddHours(24), export.ExpiresAt);
        _metrics.Verify(m => m.ExportCompleted(ExportFormatEnum.Csv, It.IsAny<TimeSpan>(), 2), Times.Once);
        _files.Verify(f => f.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Process_WhenAnotherWorkerReclaimedExport_DeletesGeneratedFile()
    {
        var export = TestData.Export(ExportStatusEnum.Processing);
        SetupClaimAndUpload(export, [TestData.Record()]);
        _exports.Setup(e => e.SaveProcessingResultAsync(export, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await ProcessHandler().ProcessNextAsync(CancellationToken.None);

        _files.Verify(f => f.DeleteAsync("file-1", It.IsAny<CancellationToken>()), Times.Once);
        _metrics.Verify(m => m.ExportCompleted(It.IsAny<ExportFormatEnum>(), It.IsAny<TimeSpan>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WhenStorageFails_AbortsUploadAndLeavesExportForRetry()
    {
        var export = TestData.Export(ExportStatusEnum.Processing);
        var upload = SetupClaimAndUpload(export, [TestData.Record()]);
        upload.Setup(u => u.CompleteAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new IOException());

        await ProcessHandler().ProcessNextAsync(CancellationToken.None);

        upload.Verify(u => u.AbortAsync(It.IsAny<CancellationToken>()), Times.Once);
        _exports.Verify(e => e.SaveProcessingResultAsync(It.IsAny<AuditExport>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(ExportStatusEnum.Processing, export.Status);
    }

    [Theory]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("+1", "'+1")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData(null, "")]
    public void CsvEscape_NeutralizesFormulasAndQuotesSpecialCharacters(string? value, string expected)
    {
        Assert.Equal(expected, CsvAuditExportFormatter.Escape(value));
    }

    private Mock<IExportFileUpload> SetupClaimAndUpload(AuditExport export, AuditRecord[] records)
    {
        _exports.Setup(e => e.TryClaimNextAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(export);

        var upload = new Mock<IExportFileUpload>();
        upload.SetupGet(u => u.Content).Returns(new MemoryStream());
        upload.Setup(u => u.CompleteAsync(It.IsAny<CancellationToken>())).ReturnsAsync("file-1");
        _files.Setup(f => f.BeginUploadAsync(export.Id, export.FileName, "text/csv", It.IsAny<CancellationToken>()))
            .ReturnsAsync(upload.Object);

        _queries.Setup(q => q.StreamAsync(export.Filter, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((AuditRecordFilter _, int limit, CancellationToken _) => records.Take(limit).ToAsyncEnumerable());

        return upload;
    }

    private void SetupExport(AuditExport export)
    {
        _exports.Setup(e => e.GetByIdAsync(TestData.TenantId, export.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(export);
    }

    private SelfAuditRecorder SelfAudit()
    {
        return new SelfAuditRecorder(_records.Object, _hasher.Object, _time);
    }

    private RequestAuditExportHandler RequestHandler(int maxRecords = 100)
    {
        return new RequestAuditExportHandler(_exports.Object, _queries.Object, SelfAudit(), new AuditQueryPolicy(),
            new AuditExportPolicy { MaxRecords = maxRecords }, _time);
    }

    private DownloadAuditExportHandler DownloadHandler()
    {
        return new DownloadAuditExportHandler(_exports.Object, _files.Object, _formatters, SelfAudit(), _time);
    }

    private ProcessAuditExportsHandler ProcessHandler(int maxRecords = 100)
    {
        return new ProcessAuditExportsHandler(_exports.Object, _queries.Object, _files.Object, _formatters,
            _metrics.Object, new AuditExportPolicy { MaxRecords = maxRecords }, _time,
            NullLogger<ProcessAuditExportsHandler>.Instance);
    }

    private static RequestAuditExportCommand Command(string format)
    {
        return new RequestAuditExportCommand
        {
            TenantId = TestData.TenantId,
            RequestedBy = "user-1",
            Format = format,
            Filter = new AuditFilterInput { From = "2026-03-01T00:00:00Z", To = "2026-03-10T00:00:00Z" }
        };
    }

    private static DownloadAuditExportQuery DownloadQuery(Guid exportId)
    {
        return new DownloadAuditExportQuery(TestData.TenantId, exportId, "user-2", "trace-1");
    }
}

internal static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
