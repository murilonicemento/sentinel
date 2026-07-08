using ChannelsService.Application.DTOs;
using ChannelsService.Domain.Entities;

namespace ChannelsService.Application.Interfaces;

public interface IRetryPolicyEngine
{
    Task<DeliveryResultDTO> ExecuteAsync(Func<CancellationToken, Task<DeliveryResultDTO>> sendFunc, int maxAttempts, ProviderResilienceDTO resilienceDto, CancellationToken cancellationToken);
}