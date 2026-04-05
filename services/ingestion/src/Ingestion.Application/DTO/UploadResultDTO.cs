using System.Net;

namespace Ingestion.Application.DTO;

public record UploadResultDTO
{
    public string ETag { get; set; } = string.Empty;
    public long Size { get; set; }
    public string ObjectName { get; set; } = string.Empty;
    public string ResponseContent { get; set; } = string.Empty;
    public HttpStatusCode ResponseStatusCode { get; set; }
}