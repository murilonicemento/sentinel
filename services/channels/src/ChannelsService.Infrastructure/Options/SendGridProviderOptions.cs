namespace ChannelsService.Infrastructure.Options;

public sealed record SendGridProviderOptions
{
    public string ApiKey { get; init; } = string.Empty;
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;
}