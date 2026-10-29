using Ingestion.Application.Interfaces.Repositories;
using Ingestion.Infrastructure.Write.Persistence.DbContext;

namespace Ingestion.Infrastructure.Write.Persistence;

public sealed class UnitOfWork(WriteDbContext context) : IUnitOfWork
{
    public async Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        var transaction = await context.BeginTransactionAsync(cancellationToken);
        try
        {
            await work(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // CancellationToken.None: the rollback must run even when the caller was cancelled.
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            await context.EndTransactionAsync();
        }
    }
}
