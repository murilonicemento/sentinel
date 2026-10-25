using ChannelsService.Domain.Entities;

namespace ChannelsService.Application.DTOs;

public sealed record DeliveryAttemptDTO(
    string AttemptId,
    string EventId,
    string TenantId,
    string Channel,
    string Provider,
    string Status,
    int AttemptCount,
    string? ErrorMessage,
    DateTime Timestamp)
{
    public static DeliveryAttemptDTO From(DeliveryAttempt attempt) => new(
        attempt.AttemptId,
        attempt.EventId,
        attempt.TenantId,
        attempt.Channel.ToString(),
        attempt.Provider,
        attempt.StatusEnum.ToString(),
        attempt.AttemptCount,
        attempt.ErrorMessage,
        attempt.Timestamp);
}

public sealed record PagedResultDTO<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalItems { get; init; }
    public int TotalPages { get; init; }
}

public sealed record ResponseBaseDTO<T>
{
    public int StatusCode { get; init; }
    public bool Success { get; init; }
    public required T Data { get; init; }
}
