using Ingestion.Application.Commands;
using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.Repositories;
using Ingestion.Domain.ValueObjects;
using MediatR;

namespace Ingestion.Application.Handlers;

public class RegisterDataSourceHandler : IRequestHandler<RegisterDataSourceCommand, (Guid dataSourceId, Guid tenantId)>
{
    private readonly IDataSourceRepository _dataSourceRepository;

    public RegisterDataSourceHandler(IDataSourceRepository dataSourceRepository)
    {
        _dataSourceRepository = dataSourceRepository;
    }

    public async Task<(Guid dataSourceId, Guid tenantId)> Handle(RegisterDataSourceCommand request, CancellationToken cancellationToken)
    {
        var dataSource = new DataSource(
            Guid.NewGuid(),
            request.Name,
            request.Endpoint,
            DataSourceType.From(request.DataSourceType).Value,
            MeasurementType.From(request.MeasurementType).Value,
            CollectionFrequencyType.From(request.CollectionFrequency).Value,
            Guid.NewGuid()
        );

        return await _dataSourceRepository.RegisterAsync(dataSource);
    }
}