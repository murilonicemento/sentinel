namespace ChannelsService.Domain.Entities;

public sealed class DeliveryResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ProviderName { get; set; }
}