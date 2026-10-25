using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Geospatial.Api.Security;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddGeospatialAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => options.TrustedServices.All(service => service.IsValid()),
                "Every Jwt:TrustedServices entry requires Name, Issuer, a SigningKey with at least 32 characters and AllowedRoles.")
            .Validate(options => options.TrustedServices.All(service => service.Issuer != options.Issuer),
                "Jwt:TrustedServices issuers must differ from the user token issuer.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                var keysByIssuer = jwt.TrustedServices.ToDictionary(
                    service => service.Issuer,
                    service => (SecurityKey)new SymmetricSecurityKey(Encoding.UTF8.GetBytes(service.SigningKey)));
                keysByIssuer[jwt.Issuer] = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));

                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuers = keysByIssuer.Keys,
                    ValidAudience = jwt.Audience,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    IssuerSigningKeyResolver = (_, securityToken, _, _) =>
                        securityToken is JsonWebToken token && keysByIssuer.TryGetValue(token.Issuer, out var key)
                            ? [key]
                            : [],
                    NameClaimType = SentinelClaims.Subject,
                    RoleClaimType = SentinelClaims.Role,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        NormalizeLegacyRoleClaims(context.Principal);

                        var issuer = context.Principal?.FindFirst(SentinelClaims.Issuer)?.Value;
                        var service = jwt.TrustedServices.FirstOrDefault(s => s.Issuer == issuer);
                        if (service is not null) DropRolesNotGrantedTo(service, context.Principal);

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(GeospatialPolicies.Read, policy => policy
                .RequireClaim(SentinelClaims.Subject)
                .RequireRole(GeospatialRoles.Admin, GeospatialRoles.Read))
            .AddPolicy(GeospatialPolicies.Write, policy => policy
                .RequireClaim(SentinelClaims.Subject)
                .RequireRole(GeospatialRoles.Admin, GeospatialRoles.Write));


        return services;
    }

    // A caller sends the same token to every service it calls, so it carries roles of other services too. Only the
    // roles this API granted to the caller in Jwt:TrustedServices are kept; any other role has no effect here.
    private static void DropRolesNotGrantedTo(TrustedServiceOptions service, ClaimsPrincipal? principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity) return;

        foreach (var role in identity.FindAll(SentinelClaims.Role).ToList())
        {
            if (!service.AllowedRoles.Contains(role.Value))
                identity.RemoveClaim(role);
        }
    }

    // Tokens issued before ADR-0002 may carry roles in the long ClaimTypes.Role claim, which the default inbound
    // claim mapping used to accept. They are copied to the canonical "role" claim so those tokens keep working.
    private static void NormalizeLegacyRoleClaims(ClaimsPrincipal? principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity) return;

        foreach (var legacyRole in identity.FindAll(ClaimTypes.Role).ToList())
        {
            if (!identity.HasClaim(SentinelClaims.Role, legacyRole.Value))
                identity.AddClaim(new Claim(SentinelClaims.Role, legacyRole.Value));
        }
    }
}
