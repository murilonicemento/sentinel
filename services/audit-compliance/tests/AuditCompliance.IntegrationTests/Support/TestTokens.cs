using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AuditCompliance.IntegrationTests.Support;

public static class TestTokens
{
    public const string Issuer = "sentinel-tests";
    public const string Audience = "sentinel-tests";
    public const string SigningKey = "integration-tests-only-signing-key-0123456789";

    public static string Create(string subject, string[] roles, Guid[] tenants, string signingKey = SigningKey)
    {
        var claims = new List<Claim> { new("sub", subject) };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        claims.AddRange(tenants.Select(tenant => new Claim("tenant_id", tenant.ToString())));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256)
        });
    }

    public static HttpClient Authorize(this HttpClient client, string token, Guid? tenantId)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (tenantId is not null) client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }
}
