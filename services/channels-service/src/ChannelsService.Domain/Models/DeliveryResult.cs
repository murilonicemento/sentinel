namespace ChannelsService.Domain.Models;

public sealed class DeliveryResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}