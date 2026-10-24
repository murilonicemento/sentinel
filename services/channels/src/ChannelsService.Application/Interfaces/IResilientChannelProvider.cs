using ChannelsService.Application.DTOs;

namespace ChannelsService.Application.Interfaces;

public interface IResilientChannelProvider
{
    ProviderResilienceDTO ResilienceOptions { get; }
}
