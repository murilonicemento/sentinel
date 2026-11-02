using System.Net.Http.Headers;

namespace RiskCatalog.Infrastructure.Security;

/// <summary>Attaches this service's token to calls made to other Sentinel services.</summary>
public sealed class ServiceTokenHandler : DelegatingHandler
{
    private readonly ServiceTokenProvider _tokenProvider;

    public ServiceTokenHandler(ServiceTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenProvider.GetToken());
        return base.SendAsync(request, cancellationToken);
    }
}
