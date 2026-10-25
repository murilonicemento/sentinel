using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ingestion.Infrastructure.Write.Security;

/// <summary>Issues short-lived service tokens and reuses each one until shortly before it expires.</summary>
public sealed class ServiceTokenProvider
{
    private static readonly TimeSpan RenewBeforeExpiry = TimeSpan.FromMinutes(1);

    private readonly ServiceTokenOptions _options;
    private readonly SigningCredentials _signingCredentials;
    private readonly TimeProvider _timeProvider;
    private readonly JsonWebTokenHandler _tokenHandler = new();
    private readonly Lock _lock = new();
    private string? _token;
    private DateTimeOffset _expiresAt;

    public ServiceTokenProvider(IOptions<ServiceTokenOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public string GetToken()
    {
        lock (_lock)
        {
            var now = _timeProvider.GetUtcNow();
            if (_token is not null && now < _expiresAt - RenewBeforeExpiry) return _token;

            _expiresAt = now.AddMinutes(_options.LifetimeMinutes);
            _token = _tokenHandler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = _expiresAt.UtcDateTime,
                Claims = new Dictionary<string, object>
                {
                    ["sub"] = _options.Issuer,
                    ["role"] = _options.Roles.ToArray()
                },
                SigningCredentials = _signingCredentials
            });

            return _token;
        }
    }
}
