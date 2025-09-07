using Ingestion.Application.Commands;
using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Repositories;
using Ingestion.Domain.ValueObjects;
using MediatR;

namespace Ingestion.Application.Handlers;

public class RegisterDataSourceHandler : IRequestHandler<RegisterDataSourceCommand, Guid>
{
    private readonly IDataSourceRepository _dataSourceRepository;

    public RegisterDataSourceHandler(IDataSourceRepository dataSourceRepository)
    {
        _dataSourceRepository = dataSourceRepository;
    }

    public async Task<Guid> Handle(RegisterDataSourceCommand request, CancellationToken cancellationToken)
    {
        var dataSource = new DataSource(
            Guid.NewGuid(),
            request.Name,
            DataSourceType.From(request.DataSourceType).Value,
            MeasurementType.From(request.MeasurementType).Value,
            request.Endpoint,
            CollectionFrequencyType.From(request.CollectionFrequency).Value
        );

        return await _dataSourceRepository.RegisterAsync(dataSource);
    }
}