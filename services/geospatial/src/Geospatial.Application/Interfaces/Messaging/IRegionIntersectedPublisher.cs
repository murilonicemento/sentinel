using Geospatial.Domain.Events;

namespace Geospatial.Application.Interfaces.Messaging;

public interface IRegionIntersectedPublisher
{
    Task PublishAsync(GeospatialOperationEvent evt, CancellationToken cancellationToken = default);
}
