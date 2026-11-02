using RiskCatalog.Domain.Entities;
using RiskCatalog.Domain.IRepositories;

namespace RiskCatalog.Application.Interfaces;

/// <summary>
/// Interface for User repository operations
/// </summary>
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetActiveUsersAsync(CancellationToken cancellationToken = default);
}
