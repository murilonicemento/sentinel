using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Ingestion.Infrastructure.Write.Persistence.DbContext;

public class WriteDbContext
{
    private readonly IConfiguration _configuration;
    public NpgsqlConnection Connection { get; }

    public WriteDbContext(IConfiguration configuration)
    {
        _configuration = configuration;
        Connection = CreateConnection();
    }

    private NpgsqlConnection CreateConnection() =>
        new(_configuration.GetConnectionString("IngestionWriteDatabase") ?? string.Empty);
}