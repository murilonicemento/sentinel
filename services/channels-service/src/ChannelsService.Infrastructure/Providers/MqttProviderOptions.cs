namespace ChannelsService.Infrastructure.Providers;

public sealed record MqttProviderOptions
{
    public string Broker { get; init; } = string.Empty;
    public int Port { get; init; } = 1883;
    public string Topic { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
}