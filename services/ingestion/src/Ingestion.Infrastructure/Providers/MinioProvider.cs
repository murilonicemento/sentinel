using System.Text;
using Ingestion.Application.Providers;
using Minio;
using Minio.DataModel.Args;
using Minio.DataModel.Response;

namespace Ingestion.Infrastructure.Providers;

public class MinioProvider : IObjectStorageProvider
{
    private readonly IMinioClient _minioClient;

    public MinioProvider(IMinioClient minioClient)
    {
        _minioClient = minioClient;
    }

    public async Task<PutObjectResponse> UploadJsonAsync(string bucketName, string objectName, string payload)
    {
        var bucketExistsArgs = new BucketExistsArgs().WithBucket(bucketName);
        var bucketExist = await _minioClient.BucketExistsAsync(bucketExistsArgs);

        if (!bucketExist)
        {
            var makeBucketArgs = new MakeBucketArgs().WithBucket(bucketName);
            await _minioClient.MakeBucketAsync(makeBucketArgs);
        }

        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        var putObjectArgs = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(memoryStream)
            .WithObjectSize(memoryStream.Length)
            .WithContentType("application/json");

        return await _minioClient.PutObjectAsync(putObjectArgs);
    }
}