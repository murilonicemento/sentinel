using Ingestion.Application.Commands;
using MediatR;

namespace Ingestion.Application.Handlers;

public class RegisterSensorCollectionHandler : IRequestHandler<RegisterSensorCollectionCommand, Guid>
{
    public async Task<Guid> Handle(RegisterSensorCollectionCommand request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}