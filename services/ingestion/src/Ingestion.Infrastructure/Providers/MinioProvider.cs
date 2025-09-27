using System.Text;
using Ingestion.Application.Interfaces.Providers;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.DataModel.Response;

namespace Ingestion.Infrastructure.Providers;

public class MinioProvider : IObjectStorageProvider
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioProvider> _logger;

    public MinioProvider(IMinioClient minioClient, ILogger<MinioProvider> logger)
    {
        _minioClient = minioClient;
        _logger = logger;
    }

    public async Task<PutObjectResponse> UploadJsonAsync(string bucketName, string objectName, string payload)
    {
        try
        {
            var bucketExistsArgs = new BucketExistsArgs().WithBucket(bucketName);
            var bucketExist = await _minioClient.BucketExistsAsync(bucketExistsArgs);

            if (!bucketExist)
            {
                var makeBucketArgs = new MakeBucketArgs().WithBucket(bucketName);

                await _minioClient.MakeBucketAsync(makeBucketArgs);
                _logger.LogInformation("Bucket with name {bucketName} created.", bucketName);
            }

            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithStreamData(memoryStream)
                .WithObjectSize(memoryStream.Length)
                .WithContentType("application/json");

            var putObject = await _minioClient.PutObjectAsync(putObjectArgs);

            _logger.LogInformation("Object with name {objectName} was uploaded to MinIO successfully.", objectName);

            return putObject;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to upload object to MinIO with name {objectName}", objectName);
            throw;
        }
    }
}