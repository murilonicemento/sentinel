using ChannelsService.Application.DTOs;
using ChannelsService.Domain.Entities;

namespace ChannelsService.Application.Interfaces;

public interface IDeliveryAttemptQueryService
{
    Task<IReadOnlyList<DeliveryAttempt>> GetByEventAsync(string eventId, TenantAccessScope scope,
        CancellationToken cancellationToken);

    /// <summary>Pages the attempts of a notification in chronological order (then by attempt id).</summary>
    Task<PagedResultDTO<DeliveryAttemptDTO>> GetPageByEventAsync(string eventId, TenantAccessScope scope, int page,
        int pageSize, CancellationToken cancellationToken);
}
