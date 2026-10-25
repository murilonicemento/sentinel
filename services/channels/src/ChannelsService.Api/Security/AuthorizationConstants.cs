namespace ChannelsService.Api.Security;

public static class ChannelsServicePolicies
{
    public const string Read = "ChannelsRead";
    public const string Write = "ChannelsWrite";
}

public static class ChannelsServiceRoles
{
    public const string Admin = "Channels.Admin";
    public const string Read = "Channels.Read";
    public const string Write = "Channels.Write";
}

public static class SentinelClaims
{
    public const string Subject = "sub";
    public const string Role = "role";
    public const string Issuer = "iss";

    /// <summary>Multi-valued claim with the tenants the identity belongs to.</summary>
    public const string TenantId = "tenant_id";
}
