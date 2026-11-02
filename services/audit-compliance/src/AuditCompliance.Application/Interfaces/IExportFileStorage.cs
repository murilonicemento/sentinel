namespace AuditCompliance.Application.Interfaces;

public interface IExportFileStorage
{
    Task<IExportFileUpload> BeginUploadAsync(Guid exportId, string fileName, string contentType,
        CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string fileId, CancellationToken cancellationToken);

    Task DeleteAsync(string fileId, CancellationToken cancellationToken);
}

public interface IExportFileUpload : IAsyncDisposable
{
    Stream Content { get; }

    /// <summary>
    /// Conclui o upload de forma durável e retorna o identificador do arquivo armazenado.
    /// </summary>
    Task<string> CompleteAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Descarta o conteúdo parcialmente enviado.
    /// </summary>
    Task AbortAsync(CancellationToken cancellationToken);
}
