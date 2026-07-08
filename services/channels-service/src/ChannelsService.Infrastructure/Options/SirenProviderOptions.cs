namespace ChannelsService.Infrastructure.Options;

public sealed record SirenProviderOptions
{
    public string Endpoint { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string DefaultDeviceId { get; init; } = string.Empty;
}