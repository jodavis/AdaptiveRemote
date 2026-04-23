using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;

namespace AdaptiveRemote.Backend.ApiTests.Support;

/// <summary>
/// Manages the lifecycle of CompiledLayoutService for API integration tests.
/// Starts the service process and captures structured log output.
///
/// A <see cref="TestJwtAuthority"/> is started before the service so that the
/// service can be configured with a real (but local) JWT authority. The
/// <see cref="HttpClient"/> exposed to tests automatically includes a valid
/// bearer token. For authentication-specific tests, use
/// <see cref="CreateToken"/> and <see cref="CreateExpiredToken"/> to build
/// tokens, and send them via <see cref="CreateAnonymousHttpClient"/> or
/// <see cref="CreateBearerHttpClient"/> directly.
/// </summary>
public class ServiceFixture : IDisposable
{
    private Process? _serviceProcess;
    private readonly StringBuilder _logOutput = new();
    private readonly object _logLock = new();
    private TestJwtAuthority? _jwtAuthority;

    public string ServiceUrl { get; }

    /// <summary>
    /// HttpClient pre-configured with a valid bearer token for the test user.
    /// </summary>
    public HttpClient HttpClient { get; private set; } = null!;

    public ServiceFixture()
    {
        ServiceUrl = $"http://localhost:{GetFreePort()}";
    }

    public async Task StartServiceAsync()
    {
        if (_serviceProcess != null)
        {
            return; // Already started
        }

        // Start the JWT authority first so its URL is available for service configuration.
        _jwtAuthority = new TestJwtAuthority();

        // Find the repository root by looking for the .git directory
        string currentDir = Directory.GetCurrentDirectory();
        string? repoRoot = currentDir;
        while (repoRoot != null && !Directory.Exists(Path.Combine(repoRoot, ".git")))
        {
            repoRoot = Directory.GetParent(repoRoot)?.FullName;
        }

        if (repoRoot == null)
        {
            throw new InvalidOperationException("Could not find repository root (no .git directory found)");
        }

        string projectPath = Path.Combine(
            repoRoot,
            "src", "AdaptiveRemote.Backend.CompiledLayoutService",
            "AdaptiveRemote.Backend.CompiledLayoutService.csproj");

        if (!File.Exists(projectPath))
        {
            throw new InvalidOperationException($"Project file not found at: {projectPath}");
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            // --no-launch-profile prevents launchSettings.json from overriding
            // ASPNETCORE_URLS with its applicationUrl setting.
            Arguments = $"run --project \"{projectPath}\" --no-build --no-launch-profile",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            Environment =
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["ASPNETCORE_URLS"] = ServiceUrl,
                // Point the service at the local test JWT authority.
                ["Cognito__Authority"] = _jwtAuthority.Authority,
                // Use the same local test authority host for LocalStack health checks.
                ["LocalStack__BaseUrl"] = _jwtAuthority.Authority,
            }
        };

        _serviceProcess = new Process { StartInfo = startInfo };

        _serviceProcess.OutputDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                lock (_logLock)
                {
                    _logOutput.AppendLine(args.Data);
                }
            }
        };

        _serviceProcess.ErrorDataReceived += (sender, args) =>
        {
            if (args.Data != null)
            {
                lock (_logLock)
                {
                    _logOutput.AppendLine($"ERROR: {args.Data}");
                }
            }
        };

        _serviceProcess.Start();
        _serviceProcess.BeginOutputReadLine();
        _serviceProcess.BeginErrorReadLine();

        // Poll /health with a temporary unauthenticated client (/health is open).
        // Use a short per-request timeout so a slow/stuck response doesn't block the loop.
        using HttpClient healthClient = new()
        {
            BaseAddress = new Uri(ServiceUrl),
            Timeout = TimeSpan.FromSeconds(5),
        };

        bool isReady = false;
        for (int i = 0; i < 30 && !_serviceProcess.HasExited; i++)
        {
            try
            {
                HttpResponseMessage response = await healthClient
                    .GetAsync("/health")
                    .ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    isReady = true;
                    break;
                }

                lock (_logLock)
                {
                    _logOutput.AppendLine($"[HealthCheck attempt {i + 1}] HTTP {(int)response.StatusCode} from {ServiceUrl}/health");
                }
            }
            catch (Exception ex)
            {
                lock (_logLock)
                {
                    _logOutput.AppendLine($"[HealthCheck attempt {i + 1}] Request failed polling {ServiceUrl}/health: {ex.Message}");
                }
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        if (!isReady)
        {
            string logs = GetLogs();
            throw new InvalidOperationException($"Service failed to start within 30 seconds (polling {ServiceUrl}/health). Logs:\n{logs}");
        }

        // Default HttpClient includes a valid bearer token for the standard test user.
        HttpClient = CreateBearerHttpClient(CreateToken());
    }

    /// <summary>
    /// Creates a valid JWT for the given subject (default: "test-user").
    /// </summary>
    public string CreateToken(string sub = "test-user")
    {
        if (_jwtAuthority is null)
        {
            throw new InvalidOperationException("StartServiceAsync() must be called before CreateToken()");
        }

        return _jwtAuthority.CreateToken(sub);
    }

    /// <summary>
    /// Creates an expired JWT.
    /// </summary>
    public string CreateExpiredToken()
    {
        if (_jwtAuthority is null)
        {
            throw new InvalidOperationException("StartServiceAsync() must be called before CreateExpiredToken()");
        }

        return _jwtAuthority.CreateExpiredToken();
    }

    /// <summary>
    /// Creates an HttpClient with no Authorization header (for testing 401 responses).
    /// </summary>
    public HttpClient CreateAnonymousHttpClient()
        => new() { BaseAddress = new Uri(ServiceUrl) };

    /// <summary>
    /// Creates an HttpClient that sends the given bearer token on every request.
    /// </summary>
    public HttpClient CreateBearerHttpClient(string token)
        => new(new BearerTokenHandler(token)) { BaseAddress = new Uri(ServiceUrl) };

    public string GetLogs()
    {
        lock (_logLock)
        {
            return _logOutput.ToString();
        }
    }

    public void Dispose()
    {
        if (_serviceProcess != null && !_serviceProcess.HasExited)
        {
            _serviceProcess.Kill(entireProcessTree: true);
            _serviceProcess.WaitForExit(5000);
            _serviceProcess.Dispose();
        }

        HttpClient?.Dispose();
        _jwtAuthority?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static int GetFreePort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Adds a bearer token to every outgoing request.
    /// </summary>
    private sealed class BearerTokenHandler : DelegatingHandler
    {
        private readonly string _token;

        public BearerTokenHandler(string token)
            : base(new HttpClientHandler())
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
}
