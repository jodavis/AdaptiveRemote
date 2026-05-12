using System.Net.Http.Headers;

namespace AdaptiveRemote.Backend.LayoutProcessingService.Services;

/// <summary>
/// DelegatingHandler that attaches a pre-configured bearer token to every outgoing request.
/// Used for service-to-service calls (e.g. LayoutProcessingService → RawLayoutService) in
/// environments where IAM-signed requests or Cognito M2M tokens are not yet wired up.
///
/// When registered with IHttpClientFactory via AddHttpMessageHandler, the factory manages
/// the inner handler; do not set InnerHandler in the constructor.
/// </summary>
public sealed class ServiceAccountTokenHandler : DelegatingHandler
{
    private readonly string _token;

    public ServiceAccountTokenHandler(string token)
    {
        _token = token;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return base.SendAsync(request, cancellationToken);
    }
}
