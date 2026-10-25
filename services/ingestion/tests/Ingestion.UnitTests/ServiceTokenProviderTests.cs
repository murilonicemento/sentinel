using System.Text;
using Ingestion.Infrastructure.Write.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ingestion.UnitTests;

public class ServiceTokenProviderTests
{
    private const string SigningKey = "unit-tests-ingestion-service-signing-key-0123456789";

    private static readonly ServiceTokenOptions Options = new()
    {
        Issuer = "sentinel-service:ingestion",
        Audience = "sentinel",
        SigningKey = SigningKey,
        Roles = ["TenantsBilling.Read", "Geospatial.Read"],
        LifetimeMinutes = 5
    };

    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));

    private ServiceTokenProvider CreateProvider() =>
        new(Microsoft.Extensions.Options.Options.Create(Options), _time);

    [Fact]
    public async Task GetToken_IssuesSignedTokenWithServiceIdentityAndRoles()
    {
        var token = CreateProvider().GetToken();

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidateLifetime = false
        });

        Assert.True(result.IsValid, result.Exception?.Message);
        var jwt = (JsonWebToken)result.SecurityToken;
        Assert.Equal(Options.Issuer, jwt.Subject);
        Assert.Equal(Options.Roles, jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value));
        Assert.Equal(_time.GetUtcNow().AddMinutes(5).UtcDateTime, jwt.ValidTo);
    }

    [Fact]
    public void GetToken_ReusesTokenUntilOneMinuteBeforeExpiry()
    {
        var provider = CreateProvider();
        var first = provider.GetToken();

        _time.Advance(TimeSpan.FromMinutes(3.5));
        Assert.Same(first, provider.GetToken());

        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.NotEqual(first, provider.GetToken());
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
