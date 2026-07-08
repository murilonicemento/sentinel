namespace ChannelsService.Infrastructure.Options;

public sealed record PushProviderOptions
{
    public string Endpoint { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string DefaultTarget { get; init; } = string.Empty;
}