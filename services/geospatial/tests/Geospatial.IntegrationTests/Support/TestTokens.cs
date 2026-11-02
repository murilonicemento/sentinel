using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Geospatial.IntegrationTests.Support;

public static class TestTokens
{
    public const string Issuer = "sentinel-tests";
    public const string Audience = "sentinel-tests";
    public const string SigningKey = "integration-tests-user-signing-key-0123456789";

    public const string ServiceName = "risk-evaluation";
    public const string ServiceIssuer = "sentinel-service:risk-evaluation";
    public const string ServiceSigningKey = "integration-tests-risk-evaluation-service-key-0123456789";

    public static string User(IEnumerable<string> roles, IEnumerable<Guid>? tenants = null) =>
        Create(Issuer, SigningKey, "user-1", roles, tenants);

    public static string Service(IEnumerable<string> roles, string signingKey = ServiceSigningKey) =>
        Create(ServiceIssuer, signingKey, ServiceName, roles, tenants: null);

    public static string Create(
        string issuer,
        string signingKey,
        string subject,
        IEnumerable<string> roles,
        IEnumerable<Guid>? tenants)
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = subject,
            ["role"] = roles.ToArray()
        };
        if (tenants is not null)
            claims["tenant_id"] = tenants.Select(id => id.ToString()).ToArray();

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = Audience,
            Claims = claims,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256)
        });
    }
}
