using System.Net;
using System.Text;
using System.Text.Json;

namespace AdaptiveRemote.EndtoEndTests.Host;

public sealed class TestJwtAuthority : ITestJwtAuthority
{
    private readonly HttpListener _listener;
    private readonly Thread _listenerThread;
    private volatile bool _stopping;

    public string Authority { get; }
    public string TokenEndpointUrl => $"{Authority}/oauth2/token";
    public string ValidClientId => "test-client-id";
    public string ValidClientSecret => "test-client-secret";

    public TestJwtAuthority()
    {
        int port = GetFreePort();
        Authority = $"http://localhost:{port}";

        _listener = new HttpListener();
        _listener.Prefixes.Add($"{Authority}/");
        _listener.Start();

        _listenerThread = new Thread(HandleRequests)
        {
            IsBackground = true,
            Name = "ClientTestJwtAuthority",
        };
        _listenerThread.Start();
    }

    private void HandleRequests()
    {
        while (!_stopping)
        {
            HttpListenerContext context;
            try
            {
                context = _listener.GetContext();
            }
            catch (HttpListenerException) when (_stopping)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            try
            {
                HandleRequest(context);
            }
            catch
            {
                // Keep the authority alive for subsequent requests.
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        string path = context.Request.Url?.AbsolutePath ?? string.Empty;
        byte[] response = path switch
        {
            "/oauth2/token" => BuildTokenResponse(context),
            "/.well-known/openid-configuration" => BuildDiscoveryDocument(),
            "/_localstack/health" => BuildHealth(),
            _ => BuildNotFound(context),
        };

        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = response.Length;
        context.Response.OutputStream.Write(response, 0, response.Length);
        context.Response.Close();
    }

    private byte[] BuildTokenResponse(HttpListenerContext context)
    {
        if (!HttpMethod.Post.Method.Equals(context.Request.HttpMethod, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            return Encoding.UTF8.GetBytes("{}");
        }

        using StreamReader reader = new(context.Request.InputStream, context.Request.ContentEncoding);
        string body = reader.ReadToEnd();
        Dictionary<string, string> form = ParseFormUrlEncoded(body);

        bool isValidGrant = form.TryGetValue("grant_type", out string? grantType)
            && string.Equals(grantType, "client_credentials", StringComparison.Ordinal);
        bool hasValidClientId = form.TryGetValue("client_id", out string? clientId)
            && string.Equals(clientId, ValidClientId, StringComparison.Ordinal);
        bool hasValidClientSecret = form.TryGetValue("client_secret", out string? clientSecret)
            && string.Equals(clientSecret, ValidClientSecret, StringComparison.Ordinal);

        if (!isValidGrant || !hasValidClientId || !hasValidClientSecret)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            return Encoding.UTF8.GetBytes("""{"error":"invalid_client"}""");
        }

        string payload = JsonSerializer.Serialize(new
        {
            access_token = "test-access-token",
            expires_in = 3600,
            token_type = "Bearer",
        });
        return Encoding.UTF8.GetBytes(payload);
    }

    private byte[] BuildDiscoveryDocument()
    {
        string payload = JsonSerializer.Serialize(new
        {
            issuer = Authority,
            token_endpoint = TokenEndpointUrl,
        });
        return Encoding.UTF8.GetBytes(payload);
    }

    private static byte[] BuildHealth() =>
        Encoding.UTF8.GetBytes("""{"status":"running"}""");

    private static byte[] BuildNotFound(HttpListenerContext context)
    {
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        return Encoding.UTF8.GetBytes("{}");
    }

    private static Dictionary<string, string> ParseFormUrlEncoded(string body) =>
        body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                static parts => WebUtility.UrlDecode(parts[0]),
                static parts => parts.Length > 1 ? WebUtility.UrlDecode(parts[1]) : string.Empty);

    private static int GetFreePort()
    {
        using System.Net.Sockets.TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        _stopping = true;
        _listener.Stop();
        _listenerThread.Join(TimeSpan.FromSeconds(2));
        _listener.Close();
    }
}
