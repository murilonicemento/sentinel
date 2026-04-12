using Dapper;
using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Infrastructure.Write.Persistence.DbContext;

namespace Ingestion.Infrastructure.Write.Persistence.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly WriteDbContext _writeDbContext;

    public TenantRepository(WriteDbContext writeDbContext)
    {
        _writeDbContext = writeDbContext;
    }

    public async Task<bool> ExistsAsync(Guid tenantId)
    {
        var query = @"SELECT 1 FROM tenant WHERE id = @TenantId AND is_active = true";
        var result =
            await _writeDbContext.Connection.QueryFirstOrDefaultAsync<int?>(query, new { TenantId = tenantId });
        return result.HasValue;
    }

    public async Task<Tenant?> GetByIdAsync(Guid tenantId)
    {
        var query = @"SELECT id, name, is_active, created_at 
                          FROM tenant 
                          WHERE id = @TenantId AND is_active = true";

        return await _writeDbContext.Connection.QueryFirstOrDefaultAsync<Tenant>(query, new { TenantId = tenantId });
    }

    public async Task<Tenant> RegisterAsync(Tenant tenant)
    {
        var query = @"INSERT INTO tenant (id, name, is_active, created_at) 
                          VALUES (@Id, @Name, @IsActive, @CreatedAt)";

        await _writeDbContext.Connection.ExecuteAsync(query, tenant);
        return tenant;
    }
}