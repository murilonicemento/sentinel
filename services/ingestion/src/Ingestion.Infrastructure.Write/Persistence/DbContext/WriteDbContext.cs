using System.Data;
using Npgsql;

namespace Ingestion.Infrastructure.Write.Persistence.DbContext;

/// <summary>
/// One Postgres connection per DI scope (request, outbox iteration, polling run), taken from the shared
/// <see cref="NpgsqlDataSource"/> pool. A connection must not be used by two operations at once, so it is never
/// shared across scopes. Repositories pass <see cref="Transaction"/> to every command, so they join the unit of work
/// when one is open.
/// </summary>
public sealed class WriteDbContext(NpgsqlDataSource dataSource) : IAsyncDisposable
{
    private NpgsqlConnection? _connection;

    public NpgsqlConnection Connection => _connection ??= dataSource.CreateConnection();

    public NpgsqlTransaction? Transaction { get; private set; }

    internal async Task<NpgsqlTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (Transaction is not null)
            throw new InvalidOperationException("A transaction is already open in this scope.");

        if (Connection.State != ConnectionState.Open)
            await Connection.OpenAsync(cancellationToken);
        Transaction = await Connection.BeginTransactionAsync(cancellationToken);
        return Transaction;
    }

    internal async Task EndTransactionAsync()
    {
        if (Transaction is null) return;
        await Transaction.DisposeAsync();
        Transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        await EndTransactionAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }
}
