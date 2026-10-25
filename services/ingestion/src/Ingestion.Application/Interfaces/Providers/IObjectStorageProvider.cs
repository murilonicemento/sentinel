using Ingestion.Application.DTO;

namespace Ingestion.Application.Interfaces.Providers;

public interface IObjectStorageProvider
{
    public Task<UploadResultDTO> UploadJsonAsync(string objectName, string payload);
}