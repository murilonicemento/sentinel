namespace ChannelsService.Application.DTOs;

public sealed class DeliveryResultDTO
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ProviderName { get; set; }
}