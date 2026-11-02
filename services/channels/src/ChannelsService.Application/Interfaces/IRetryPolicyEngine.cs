using ChannelsService.Application.DTOs;

namespace ChannelsService.Application.Interfaces;

public interface IRetryPolicyEngine
{
    /// <summary>
    /// Sends through <paramref name="sendFunc"/>, retrying only transient failures. <paramref name="maxAttempts"/> is
    /// the total number of attempts including the first, capped by the platform limit.
    /// </summary>
    Task<DeliveryResultDTO> ExecuteAsync(string providerName, Func<CancellationToken, Task<DeliveryResultDTO>> sendFunc,
        int maxAttempts, ProviderResilienceDTO resilienceDto, CancellationToken cancellationToken);
}
