using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;

namespace AdaptiveRemote.EndToEndTests.TestServices.Backend;

/// <summary>
/// Manages the lifecycle of backend services for API integration tests.
/// Starts the service process and captures structured log output.
/// </summary>
public class ServiceFixture : IDisposable
{
    private Process? _serviceProcess;
    private readonly StringBuilder _logOutput = new();
    private readonly object _logLock = new();
    private readonly string _serviceName;
    private readonly ISimulatedEnvironment _environment;
    private readonly IReadOnlyDictionary<string, string>? _environmentVariables;

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
    }

    public async Task StartServiceAsync()
    {
        if (_serviceProcess != null)
        {
            return; // Already started
        }

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
            "src", _serviceName,
            $"{_serviceName}.csproj");

        if (!File.Exists(projectPath))
        {
            throw new InvalidOperationException($"Project file not found at: {projectPath}");
        }

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
    }

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
