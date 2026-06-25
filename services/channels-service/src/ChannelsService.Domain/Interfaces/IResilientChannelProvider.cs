using ChannelsService.Domain.Entities;

namespace ChannelsService.Domain.Interfaces;

public interface IResilientChannelProvider
{
    ProviderResilienceOptions ResilienceOptions { get; }
}
