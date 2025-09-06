using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Ingestion.Infrastructure.DbContext;

public class IngestionDbContext
{
    private readonly IConfiguration _configuration;
    public NpgsqlConnection Connection { get; }

    public IngestionDbContext(IConfiguration configuration)
    {
        _configuration = configuration;
        Connection = CreateConnection();
    }

    private NpgsqlConnection CreateConnection() =>
        new(_configuration.GetConnectionString("IngestionDatabase") ?? string.Empty);
}