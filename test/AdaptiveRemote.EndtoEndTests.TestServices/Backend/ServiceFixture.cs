using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;

namespace AdaptiveRemote.EndToEndTests.TestServices.Backend;

/// <summary>
/// Manages the lifecycle of backend services for API integration tests.
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
    // LocalStack is shared across all scenarios to avoid repeated slow startups.
    // Data isolation is achieved via unique per-scenario user IDs.
    private static LocalStackFixture? _sharedLocalStack;
    private static readonly SemaphoreSlim _localStackInitLock = new(1, 1);

    private Process? _serviceProcess;
    private readonly StringBuilder _logOutput = new();
    private readonly object _logLock = new();
    private TestJwtAuthority? _jwtAuthority;
    private string? _startedServiceName;

    // Use a unique user ID per fixture so each scenario operates on isolated data
    // even when DynamoDB is shared across test scenarios via the shared LocalStack.
    private readonly string _testUserId = $"test-user-{Guid.NewGuid():N}";

    public string ServiceUrl { get; }

    /// <summary>
    /// HttpClient pre-configured with a valid bearer token for the test user.
    /// </summary>
    public HttpClient HttpClient { get; private set; } = null!;

    public ServiceFixture()
    {
        ServiceUrl = $"http://localhost:{GetFreePort()}";
    }

    public async Task StartServiceAsync(string serviceName = "AdaptiveRemote.Backend.CompiledLayoutService")
    {
        if (_serviceProcess != null)
        {
            if (_startedServiceName != serviceName)
            {
                throw new InvalidOperationException($"Service fixture already started with {_startedServiceName}, cannot start {serviceName}");
            }
            return; // Already started
        }

        _startedServiceName = serviceName;

        // Start LocalStack if this is a service that needs AWS resources (DynamoDB, SQS).
        // A single LocalStack instance is shared across all scenarios.
        LocalStackFixture? localStack = null;
        if (serviceName == "AdaptiveRemote.Backend.RawLayoutService"
            || serviceName == "AdaptiveRemote.Backend.LayoutProcessingService")
        {
            localStack = await GetSharedLocalStackAsync();

            if (serviceName == "AdaptiveRemote.Backend.RawLayoutService")
            {
                await localStack.CreateTableAsync("RawLayouts");
            }

            if (serviceName == "AdaptiveRemote.Backend.LayoutProcessingService")
            {
                await localStack.CreateSqsQueueAsync("LayoutProcessingQueue");
            }
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
            "src", serviceName,
            $"{serviceName}.csproj");

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

        // Configure AWS resources for services that need LocalStack
        if (localStack != null)
        {
            // Provide dummy AWS credentials for LocalStack
            startInfo.Environment["AWS_ACCESS_KEY_ID"] = "test";
            startInfo.Environment["AWS_SECRET_ACCESS_KEY"] = "test";
        }

        // Configure DynamoDB for RawLayoutService
        if (serviceName == "AdaptiveRemote.Backend.RawLayoutService" && localStack != null)
        {
            startInfo.Environment["DynamoDB__ServiceUrl"] = localStack.ServiceUrl;
            startInfo.Environment["DynamoDB__Region"] = localStack.Region;
            startInfo.Environment["DynamoDB__TableName"] = "RawLayouts";
            startInfo.Environment["Sqs__ServiceUrl"] = localStack.ServiceUrl;
            startInfo.Environment["Sqs__QueueUrl"] = localStack.GetSqsQueueUrl("LayoutProcessingQueue");
            startInfo.Environment["Sqs__Region"] = localStack.Region;
        }

        // Configure SQS for LayoutProcessingService
        if (serviceName == "AdaptiveRemote.Backend.LayoutProcessingService" && localStack != null)
        {
            startInfo.Environment["Sqs__ServiceUrl"] = localStack.ServiceUrl;
            startInfo.Environment["Sqs__QueueUrl"] = localStack.GetSqsQueueUrl("LayoutProcessingQueue");
            startInfo.Environment["Sqs__Region"] = localStack.Region;
            // Disable the SQS polling background service so health-check-only tests do not
            // trigger the orchestration pipeline and log errors against unconfigured upstreams.
            startInfo.Environment["Orchestrator__Enabled"] = "false";
        }

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

        // Default HttpClient includes a valid bearer token for the scenario-unique test user.
        HttpClient = CreateBearerHttpClient(CreateToken());
    }

    /// <summary>
    /// Creates a valid JWT for the given subject. Defaults to the scenario-unique test user
    /// to ensure each scenario operates on isolated DynamoDB data.
    /// </summary>
    public string CreateToken(string? sub = null)
    {
        if (_jwtAuthority is null)
        {
            throw new InvalidOperationException("StartServiceAsync() must be called before CreateToken()");
        }

        return _jwtAuthority.CreateToken(sub ?? _testUserId);
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
        // LocalStack is shared across all scenarios; do not dispose it here.
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

    private static async Task<LocalStackFixture> GetSharedLocalStackAsync()
    {
        await _localStackInitLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_sharedLocalStack == null)
            {
                LocalStackFixture localStack = new();
                await localStack.StartAsync().ConfigureAwait(false);
                _sharedLocalStack = localStack;
            }

            return _sharedLocalStack;
        }
        finally
        {
            _localStackInitLock.Release();
        }
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
