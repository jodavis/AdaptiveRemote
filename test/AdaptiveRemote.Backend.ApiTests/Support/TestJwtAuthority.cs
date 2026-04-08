using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace AdaptiveRemote.Backend.ApiTests.Support;

/// <summary>
/// A minimal local OIDC/JWKS authority used by API integration tests to issue and
/// validate JWTs without a real Cognito user pool.
///
/// Exposes two endpoints on a dynamically-assigned localhost port:
///   GET /.well-known/openid-configuration  — OIDC discovery document
///   GET /.well-known/jwks.json             — RSA public key in JWK format
///
/// The service under test is configured to use this authority via the
/// Cognito__Authority environment variable so that bearer token validation
/// is exercised end-to-end without external dependencies.
/// </summary>
public sealed class TestJwtAuthority : IDisposable
{
    private const string TestAudience = "api-tests";

    private readonly RSA _rsa;
    private readonly RsaSecurityKey _signingKey;
    private readonly string _keyId;
    private readonly HttpListener _listener;
    private readonly Thread _listenerThread;
    private volatile bool _stopping;

    /// <summary>The authority URL the service under test should be configured with.</summary>
    public string Authority { get; }

    public TestJwtAuthority()
    {
        int port = GetFreePort();
        Authority = $"http://localhost:{port}";

        _rsa = RSA.Create(2048);
        _keyId = Guid.NewGuid().ToString("N")[..8];
        _signingKey = new RsaSecurityKey(_rsa) { KeyId = _keyId };

        _listener = new HttpListener();
        _listener.Prefixes.Add($"{Authority}/");
        _listener.Start();

        _listenerThread = new Thread(HandleRequests) { IsBackground = true, Name = "TestJwtAuthority" };
        _listenerThread.Start();
    }

    /// <summary>
    /// Creates a signed JWT with the given subject claim, valid for one hour.
    /// </summary>
    public string CreateToken(string sub)
        => CreateTokenCore(sub, expired: false);

    /// <summary>
    /// Creates a signed JWT that is already expired (issued/expiry in the past).
    /// </summary>
    public string CreateExpiredToken(string sub = "test-user")
        => CreateTokenCore(sub, expired: true);

    private string CreateTokenCore(string sub, bool expired)
    {
        DateTime now = DateTime.UtcNow;
        DateTime notBefore = expired ? now.AddHours(-2) : now.AddSeconds(-5);
        DateTime expires = expired ? now.AddHours(-1) : now.AddHours(1);

        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken token = handler.CreateJwtSecurityToken(
            issuer: Authority,
            audience: TestAudience,
            subject: new ClaimsIdentity([new Claim("sub", sub)]),
            notBefore: notBefore,
            expires: expires,
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256));

        return handler.WriteToken(token);
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
                // Do not crash the listener thread on individual request errors.
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        string path = context.Request.Url?.AbsolutePath ?? string.Empty;
        context.Response.ContentType = "application/json";

        byte[] body = path switch
        {
            "/.well-known/openid-configuration" => BuildDiscoveryDocument(),
            "/.well-known/jwks.json" => BuildJwks(),
            _ => BuildNotFound(context),
        };

        context.Response.ContentLength64 = body.Length;
        context.Response.OutputStream.Write(body, 0, body.Length);
        context.Response.Close();
    }

    private byte[] BuildDiscoveryDocument()
    {
        string json = JsonSerializer.Serialize(new
        {
            issuer = Authority,
            jwks_uri = $"{Authority}/.well-known/jwks.json",
            token_endpoint = $"{Authority}/oauth2/token",
        });
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    private byte[] BuildJwks()
    {
        RSAParameters rsaParams = _rsa.ExportParameters(includePrivateParameters: false);

        string n = Base64UrlEncoder.Encode(rsaParams.Modulus!);
        string e = Base64UrlEncoder.Encode(rsaParams.Exponent!);

        string json = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = _keyId,
                    n,
                    e,
                }
            }
        });
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    private static byte[] BuildNotFound(HttpListenerContext context)
    {
        context.Response.StatusCode = 404;
        return System.Text.Encoding.UTF8.GetBytes("{}");
    }

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
        _listenerThread.Join(timeout: TimeSpan.FromSeconds(2));
        _listener.Close();
        _rsa.Dispose();
    }
}
