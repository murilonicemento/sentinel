using Minio.DataModel.Response;

namespace Ingestion.Application.Interfaces.Providers;

public interface IObjectStorageProvider
{
    public Task<PutObjectResponse> UploadJsonAsync(string bucketName, string objectName, string payload);
}