using ChannelsService.Domain.Models;

namespace ChannelsService.Infrastructure.Providers;

public sealed class ChannelProviderSettings<TOptions>
    where TOptions : class, new()
{
    public string Type { get; set; } = string.Empty;
    public TOptions Options { get; set; } = new();
    public ProviderResilienceOptions Resilience { get; set; } = new();
}

public sealed record SendGridProviderOptions
{
    public string ApiKey { get; init; } = string.Empty;
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;
}

public sealed record TwilioProviderOptions
{
    public string AccountSid { get; init; } = string.Empty;
    public string AuthToken { get; init; } = string.Empty;
    public string FromNumber { get; init; } = string.Empty;
}

public sealed record MqttProviderOptions
{
    public string Broker { get; init; } = string.Empty;
    public int Port { get; init; } = 1883;
    public string Topic { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
}
