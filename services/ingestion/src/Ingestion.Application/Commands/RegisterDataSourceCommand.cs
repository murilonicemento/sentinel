using Ingestion.Domain.Enums;
using MediatR;

namespace Ingestion.Application.Commands;

public record RegisterDataSourceCommand(
    string Name,
    DataSourceTypeEnum Type,
    string Endpoint,
    CollectionFrequencyEnum CollectionFrequency
) : IRequest<Guid>;