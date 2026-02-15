namespace Geospatial.Domain.Repositories;

public interface IOutboxRepository
{
    public Task<bool> ExistsPending(Guid aggregateId);
    public Task<bool> UpdateProcessed(Guid id);
}