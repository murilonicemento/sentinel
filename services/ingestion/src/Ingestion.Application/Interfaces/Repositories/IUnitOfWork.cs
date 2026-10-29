namespace Ingestion.Application.Interfaces.Repositories;

/// <summary>Runs repository writes of the current scope in one database transaction (commit or rollback as a whole).</summary>
public interface IUnitOfWork
{
    Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken);
}
