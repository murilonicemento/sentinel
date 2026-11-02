using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Geospatial.Infrastructure.Persistence.DbContext;

public class ApplicationDbContext
{
    private readonly IConfiguration _configuration;
    public NpgsqlConnection Connection { get; }

    public ApplicationDbContext(IConfiguration configuration)
    {
        _configuration = configuration;
        Connection = CreateConnection();
    }

    private NpgsqlConnection CreateConnection() =>
        new(_configuration.GetConnectionString("IngestionWriteDatabase") ?? string.Empty);
}