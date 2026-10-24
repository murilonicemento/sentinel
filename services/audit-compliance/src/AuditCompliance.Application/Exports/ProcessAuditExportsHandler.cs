using System.Diagnostics;
using AuditCompliance.Application.Common;
using AuditCompliance.Application.Exports.Formatters;
using AuditCompliance.Application.Interfaces;
using AuditCompliance.Application.Policies;
using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace AuditCompliance.Application.Exports;

/// <summary>
/// Gera os arquivos das exportações pendentes e remove os arquivos expirados.
/// Uma exportação que falhe por erro transitório não é marcada como falha: o lease expira e ela é
/// reivindicada novamente até o limite de tentativas.
/// </summary>
public sealed class ProcessAuditExportsHandler
{
    private const int ExpirationBatchSize = 50;

    private readonly IAuditExportRepository _exportRepository;
    private readonly IExportFileStorage _fileStorage;
    private readonly IEnumerable<IAuditExportFormatter> _formatters;
    private readonly ILogger<ProcessAuditExportsHandler> _logger;
    private readonly IAuditMetrics _metrics;
    private readonly AuditExportPolicy _policy;
    private readonly IAuditRecordQueryRepository _queryRepository;
    private readonly TimeProvider _timeProvider;

    public ProcessAuditExportsHandler(
        IAuditExportRepository exportRepository,
        IAuditRecordQueryRepository queryRepository,
        IExportFileStorage fileStorage,
        IEnumerable<IAuditExportFormatter> formatters,
        IAuditMetrics metrics,
        AuditExportPolicy policy,
        TimeProvider timeProvider,
        ILogger<ProcessAuditExportsHandler> logger)
    {
        _exportRepository = exportRepository;
        _queryRepository = queryRepository;
        _fileStorage = fileStorage;
        _formatters = formatters;
        _metrics = metrics;
        _policy = policy;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Processa uma exportação pendente. Retorna false quando não há exportações a processar.
    /// </summary>
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var abandoned = await _exportRepository.FailAbandonedAsync(now, _policy.MaxProcessingAttempts,
            ApplicationErrorCodes.ExportProcessingAbandoned, cancellationToken);

        for (var i = 0; i < abandoned; i++) _metrics.ExportFailed(ApplicationErrorCodes.ExportProcessingAbandoned);

        var export = await _exportRepository.TryClaimNextAsync(now, now.Add(_policy.ProcessingLease),
            _policy.MaxProcessingAttempts, cancellationToken);

        if (export is null) return false;

        await GenerateAsync(export, cancellationToken);
        return true;
    }

    public async Task<int> ExpireFilesAsync(CancellationToken cancellationToken)
    {
        var expired = await _exportRepository.GetExpiredCompletedAsync(_timeProvider.GetUtcNow(), ExpirationBatchSize,
            cancellationToken);

        foreach (var export in expired)
        {
            if (export.FileId is not null) await _fileStorage.DeleteAsync(export.FileId, cancellationToken);

            await _exportRepository.MarkExpiredAsync(export.Id, cancellationToken);
            _logger.LogInformation("Export {ExportId} expired and its file was removed", export.Id);
        }

        return expired.Count;
    }

    private async Task GenerateAsync(AuditExport export, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var formatter = _formatters.Single(f => f.Format == export.Format);
        var limiter = new RecordLimiter(_policy.MaxRecords);

        await using var upload =
            await _fileStorage.BeginUploadAsync(export.Id, export.FileName, formatter.ContentType, cancellationToken);

        try
        {
            var records = limiter.Apply(
                _queryRepository.StreamAsync(export.Filter, _policy.MaxRecords + 1, cancellationToken));

            var count = await formatter.WriteAsync(records, upload.Content, cancellationToken);

            if (limiter.Exceeded)
            {
                await upload.AbortAsync(cancellationToken);
                await FailAsync(export, ApplicationErrorCodes.ExportLimitExceeded, cancellationToken);
                return;
            }

            var fileId = await upload.CompleteAsync(cancellationToken);
            var completedAt = _timeProvider.GetUtcNow();
            export.Complete(fileId, count, completedAt, completedAt.Add(_policy.FileExpiration));

            if (!await _exportRepository.SaveProcessingResultAsync(export, cancellationToken))
            {
                _logger.LogWarning("Export {ExportId} was reclaimed by another worker; discarding generated file",
                    export.Id);
                await _fileStorage.DeleteAsync(fileId, cancellationToken);
                return;
            }

            _metrics.ExportCompleted(export.Format, stopwatch.Elapsed, count);
            _logger.LogInformation("Export {ExportId} completed with {RecordCount} records in {ElapsedMs} ms",
                export.Id, count, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _metrics.ExportFailed("PROCESSING_ERROR");
            _logger.LogError(exception, "Export {ExportId} failed on attempt {Attempt}; it will be retried after the lease expires",
                export.Id, export.Attempts);
            await upload.AbortAsync(CancellationToken.None);
        }
    }

    private async Task FailAsync(AuditExport export, string failureCode, CancellationToken cancellationToken)
    {
        export.Fail(failureCode, _timeProvider.GetUtcNow());
        await _exportRepository.SaveProcessingResultAsync(export, cancellationToken);

        _metrics.ExportFailed(failureCode);
        _logger.LogWarning("Export {ExportId} failed with code {FailureCode}", export.Id, failureCode);
    }

    private sealed class RecordLimiter(int maxRecords)
    {
        public bool Exceeded { get; private set; }

        public async IAsyncEnumerable<AuditRecord> Apply(IAsyncEnumerable<AuditRecord> source)
        {
            var count = 0;
            await foreach (var record in source)
            {
                if (++count > maxRecords)
                {
                    Exceeded = true;
                    yield break;
                }

                yield return record;
            }
        }
    }
}
