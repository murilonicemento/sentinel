using Ingestion.Application.Interfaces.Repositories;

namespace Ingestion.UnitTests;

/// <summary>Runs the work directly and records each call, standing in for the database transaction.</summary>
public sealed class InlineUnitOfWork : IUnitOfWork
{
    public int Executions { get; private set; }

    public async Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        Executions++;
        await work(cancellationToken);
    }
}
