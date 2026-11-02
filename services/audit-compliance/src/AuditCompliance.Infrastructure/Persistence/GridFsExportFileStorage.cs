using AuditCompliance.Application.Interfaces;
using MongoDB.Bson;
using MongoDB.Driver.GridFS;

namespace AuditCompliance.Infrastructure.Persistence;

/// <summary>
/// Armazena os arquivos de exportação no GridFS do próprio banco do serviço, com acesso restrito
/// à API autenticada. Os arquivos são removidos pelo worker quando a exportação expira.
/// </summary>
public sealed class GridFsExportFileStorage : IExportFileStorage
{
    private readonly IGridFSBucket _bucket;

    public GridFsExportFileStorage(AuditComplianceMongoContext context)
    {
        _bucket = context.ExportFiles;
    }

    public async Task<IExportFileUpload> BeginUploadAsync(Guid exportId, string fileName, string contentType,
        CancellationToken cancellationToken)
    {
        var stream = await _bucket.OpenUploadStreamAsync(fileName, new GridFSUploadOptions
        {
            Metadata = new BsonDocument
            {
                { "exportId", exportId.ToString() },
                { "contentType", contentType }
            }
        }, cancellationToken);

        return new GridFsExportFileUpload(_bucket, stream);
    }

    public async Task<Stream> OpenReadAsync(string fileId, CancellationToken cancellationToken)
    {
        return await _bucket.OpenDownloadStreamAsync(ObjectId.Parse(fileId), cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string fileId, CancellationToken cancellationToken)
    {
        try
        {
            await _bucket.DeleteAsync(ObjectId.Parse(fileId), cancellationToken);
        }
        catch (GridFSFileNotFoundException)
        {
            // A remoção é idempotente: o arquivo já pode ter sido removido por outra instância.
        }
    }

    private sealed class GridFsExportFileUpload : IExportFileUpload
    {
        private readonly IGridFSBucket _bucket;
        private readonly GridFSUploadStream _stream;
        private bool _aborted;
        private bool _completed;

        public GridFsExportFileUpload(IGridFSBucket bucket, GridFSUploadStream stream)
        {
            _bucket = bucket;
            _stream = stream;
        }

        public Stream Content => _stream;

        public async Task<string> CompleteAsync(CancellationToken cancellationToken)
        {
            await _stream.CloseAsync(cancellationToken);
            _completed = true;
            return _stream.Id.ToString();
        }

        public async Task AbortAsync(CancellationToken cancellationToken)
        {
            if (_aborted) return;
            _aborted = true;

            if (_completed)
            {
                try
                {
                    await _bucket.DeleteAsync(_stream.Id, cancellationToken);
                }
                catch (GridFSFileNotFoundException)
                {
                }

                return;
            }

            await _stream.AbortAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_completed) await AbortAsync(CancellationToken.None);

            _stream.Dispose();
        }
    }
}
