namespace ChannelsService.Infrastructure.Options;

public sealed record TwilioProviderOptions
{
    public string AccountSid { get; init; } = string.Empty;
    public string AuthToken { get; init; } = string.Empty;
    public string FromNumber { get; init; } = string.Empty;
}