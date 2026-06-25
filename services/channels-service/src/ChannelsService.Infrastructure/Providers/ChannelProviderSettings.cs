using ChannelsService.Domain.Entities;

namespace ChannelsService.Infrastructure.Providers;

public sealed class ChannelProviderSettings<TOptions>
    where TOptions : class, new()
{
    public string Type { get; set; } = string.Empty;
    public TOptions Options { get; set; } = new();
    public ProviderResilienceOptions Resilience { get; set; } = new();
}