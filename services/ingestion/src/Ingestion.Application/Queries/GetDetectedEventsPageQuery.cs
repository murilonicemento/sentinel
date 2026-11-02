using Ingestion.Application.DTO;
using MediatR;

namespace Ingestion.Application.Queries;

public sealed record GetDetectedEventsPageQuery(TenantAccessScope Scope, int Page, int PageSize)
    : IRequest<PagedResultDTO<DetectedEventDTO>>;

public sealed record GetDetectedEventStatisticsQuery(TenantAccessScope Scope, DateTime? InitialDate, DateTime? EndDate)
    : IRequest<DetectedEventStatisticsDTO>;
