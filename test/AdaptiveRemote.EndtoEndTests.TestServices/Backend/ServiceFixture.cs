using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;
using AdaptiveRemote.TestUtilities;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndToEndTests.TestServices.Backend;
/// <summary>
/// Manages the lifecycle of backend services for API integration tests.
/// Starts the service process and captures structured log output.
/// </summary>
public class ServiceFixture : IDisposable
{
    private Process? _serviceProcess;
    private readonly string _serviceName;
    private readonly ISimulatedEnvironment _environment;
    private readonly IReadOnlyDictionary<string, string>? _environmentVariables;
    private readonly ILogger _logger;

    public string? LogFilePath { get; }

    public string ServiceUrl { get; }

    public ServiceFixture(string serviceName, ISimulatedEnvironment environment, Dictionary<string, string>? environmentVariables = null)
    {
        _environmentVariables = environmentVariables;
        ServiceUrl = $"http://localhost:{GetFreePort()}";
        _serviceName = serviceName;
        _environment = environment;

        LogFilePath = _environment.LogFolder is null
            ? null
            : Path.Combine(_environment.LogFolder, $"{serviceName}_{DateTime.Now:yyyyMMdd_HHmmss}.log)");

        _logger = environment.LoggerFactory.CreateLogger(serviceName + "Fixture");
    }

    public void StartService()
    {
        if (_serviceProcess != null)
        {
            return; // Already started
        }

        _logger.LogInformation("Initializing {ServiceName} fixture", _serviceName);

        // Find the repository root by looking for the .git directory
        string currentDir = Directory.GetCurrentDirectory();
        string? repoRoot = currentDir;
        while (repoRoot != null && !Path.Exists(Path.Combine(repoRoot, ".git")))
        {
            repoRoot = Directory.GetParent(repoRoot)?.FullName;
        }

        if (repoRoot == null)
        {
            _logger.LogError("Could not find repository root (no .git directory found)");
            throw new InvalidOperationException("Could not find repository root (no .git directory found)");
        }

        string projectPath = Path.Combine(
            repoRoot,
            "src", _serviceName,
            $"{_serviceName}.csproj");

        if (!File.Exists(projectPath))
        {
            _logger.LogError("Project file not found at: {ProjectPath}", projectPath);
            throw new InvalidOperationException($"Project file not found at: {projectPath}");
        }

        _logger.LogInformation("Found project file for {ServiceName} at: {ProjectPath}", _serviceName, projectPath);

        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            // --no-launch-profile prevents launchSettings.json from overriding
            // ASPNETCORE_URLS with its applicationUrl setting.
            Arguments = $"run --project \"{projectPath}\" --no-build --no-launch-profile --logFile \"{LogFilePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            Environment =
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["ASPNETCORE_URLS"] = ServiceUrl,
                // Point the service at the local test JWT authority.
                ["Cognito__Authority"] = _environment.JwtAuthority.Authority,
                // Use the same local test authority host for LocalStack health checks.
                ["LocalStack__BaseUrl"] = _environment.JwtAuthority.Authority,

                // Configure AWS resources for services that need LocalStack
                ["AWS_ACCESS_KEY_ID"] = "test",
                ["AWS_SECRET_ACCESS_KEY"] = "test",

                // ASP.NET Core Data Protection logs a development-only warning about
                // unencrypted key persistence that is unrelated to the service behavior
                // these API tests are exercising. Treat it as non-actionable noise so
                // log-cleanliness assertions remain focused on service regressions.
                ["Logging__LogLevel__Microsoft.AspNetCore.DataProtection"] = "Error",

                // Disable the SQS polling background service so health-check-only tests do not
                // trigger the orchestration pipeline and log errors against unconfigured upstreams.
                ["Orchestrator__Enabled"] = "false",
            }
        };

        if (_serviceName == "AdaptiveRemote.Backend.RawLayoutService")
        {
            // Configure DynamoDB for RawLayoutService
            startInfo.Environment["DynamoDB__ServiceUrl"] = _environment.LocalStack.ServiceUrl;
            startInfo.Environment["DynamoDB__Region"] = _environment.LocalStack.Region;
            startInfo.Environment["DynamoDB__TableName"] = "RawLayouts";
        }

        if (_serviceName == "AdaptiveRemote.Backend.LayoutProcessingService")
        {
            // Configure SQS for LayoutProcessingService
            startInfo.Environment["Sqs__ServiceUrl"] = _environment.LocalStack.ServiceUrl;
            startInfo.Environment["Sqs__QueueUrl"] = _environment.LocalStack.GetSqsQueueUrl("LayoutProcessingQueue");
            startInfo.Environment["Sqs__Region"] = _environment.LocalStack.Region;
        }

        if (_environmentVariables is not null)
        {
            foreach (KeyValuePair<string, string> envVar in _environmentVariables)
            {
                startInfo.Environment.Add(envVar.Key, envVar.Value);
            }
        }

        _serviceProcess = new Process { StartInfo = startInfo };
        _serviceProcess.Start();

        // Poll /health with a temporary unauthenticated client (/health is open).
        // Use a short per-request timeout so a slow/stuck response doesn't block the loop.
        using HttpClient healthClient = new()
        {
            BaseAddress = new Uri(ServiceUrl),
            Timeout = TimeSpan.FromSeconds(5),
        };

        int i = 0;
        bool isReady = WaitHelpers.ExecuteWithRetries(() =>
        {
            try
            {
                HttpResponseMessage response = WaitHelpers.WaitForAsyncTask(ct => healthClient.GetAsync("/health", ct));
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                _logger.LogWarning("Health check attempt {Attempt} failed with HTTP {StatusCode} from {ServiceUrl}/health", ++i, (int)response.StatusCode, ServiceUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health check attempt {Attempt} failed polling {ServiceUrl}/health", ++i, ServiceUrl);
            }

            return false;
        });

        if (!isReady)
        {
            _logger.LogError("Service failed to start within 30 seconds (polling {ServiceUrl}/health).", ServiceUrl);
            throw new InvalidOperationException($"Service failed to start within 30 seconds (polling {ServiceUrl}/health).");
        }

        _logger.LogInformation("{ServiceName} is ready and responding to health checks at {ServiceUrl}/health", _serviceName, ServiceUrl);
    }

    public void Dispose()
    {
        if (_serviceProcess != null && !_serviceProcess.HasExited)
        {
            _serviceProcess.Kill(entireProcessTree: true);
            _serviceProcess.WaitForExit(5000);
            _serviceProcess.Dispose();
        }

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
}
