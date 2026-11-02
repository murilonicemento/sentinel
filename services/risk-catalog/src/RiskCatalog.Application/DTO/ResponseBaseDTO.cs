namespace RiskCatalog.Application.DTO;

public record ResponseBaseDTO<T>
{
    public int StatusCode { get; set; }
    public bool Success { get; set; }
    public required T Data { get; set; }
}