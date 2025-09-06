using Ingestion.Application.Commands;
using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Repositories;
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
        var dataSource = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = Convert.ToInt32(request.Type),
            Endpoint = request.Endpoint,
            CollectionFrequency = Convert.ToInt32(request.CollectionFrequency)
        };

        return await _dataSourceRepository.Register(dataSource);
    }
}