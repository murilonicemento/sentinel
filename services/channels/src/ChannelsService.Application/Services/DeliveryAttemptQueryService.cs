using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Entities;

namespace ChannelsService.Application.Services;

public sealed class DeliveryAttemptQueryService : IDeliveryAttemptQueryService
{
    private readonly IDeliveryRepository _deliveryRepository;

    public DeliveryAttemptQueryService(IDeliveryRepository deliveryRepository)
    {
        _deliveryRepository = deliveryRepository;
    }

    public async Task<IReadOnlyList<DeliveryAttempt>> GetByEventAsync(string eventId, TenantAccessScope scope,
        CancellationToken cancellationToken)
    {
        var attempts = await _deliveryRepository.GetByEventAsync(eventId);
        return attempts.Where(attempt => scope.Allows(attempt.TenantId)).ToList();
    }

    public async Task<PagedResultDTO<DeliveryAttemptDTO>> GetPageByEventAsync(string eventId, TenantAccessScope scope,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var attempts = (await GetByEventAsync(eventId, scope, cancellationToken))
            .OrderBy(attempt => attempt.Timestamp)
            .ThenBy(attempt => attempt.AttemptId, StringComparer.Ordinal)
            .ToList();

        return new PagedResultDTO<DeliveryAttemptDTO>
        {
            Items = attempts.Skip((page - 1) * pageSize).Take(pageSize).Select(DeliveryAttemptDTO.From).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = attempts.Count,
            TotalPages = (int)Math.Ceiling(attempts.Count / (double)pageSize)
        };
    }
}
