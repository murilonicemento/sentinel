namespace ChannelsService.Application.DTOs;

public sealed class DeliveryResultDTO
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ProviderName { get; set; }

    /// <summary>
    /// Whether a failed delivery may succeed if retried (provider unavailable, timeout, throttling). Permanent
    /// failures such as a missing recipient or a rejected request are not retried.
    /// </summary>
    public bool IsTransient { get; set; }
}