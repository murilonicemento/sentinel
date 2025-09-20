using Minio.DataModel.Response;

namespace Ingestion.Application.Providers;

public interface IObjectStorageProvider
{
    public Task<PutObjectResponse> UploadJsonAsync(string bucketName, string objectName, string payload);
}