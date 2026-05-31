using ChannelsService.Domain.Models;

namespace ChannelsService.Domain.Interfaces;

public interface IResilientChannelProvider
{
    ProviderResilienceOptions ResilienceOptions { get; }
}
