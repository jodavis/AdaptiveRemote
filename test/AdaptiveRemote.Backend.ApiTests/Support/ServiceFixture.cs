using System.Diagnostics;
using System.Text;

namespace AdaptiveRemote.Backend.ApiTests.Support;

/// <summary>
/// Manages the lifecycle of CompiledLayoutService for API integration tests.
/// Starts the service process and captures structured log output.
/// </summary>
public class ServiceFixture : IDisposable
{
    private Process? _serviceProcess;
    private readonly StringBuilder _logOutput = new();
    private readonly object _logLock = new();

    public string ServiceUrl { get; private set; } = "http://localhost:5000";
    public HttpClient HttpClient { get; private set; } = null!;

    public void StartService()
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
            "src", "AdaptiveRemote.Backend.CompiledLayoutService",
            "AdaptiveRemote.Backend.CompiledLayoutService.csproj");

        if (!File.Exists(projectPath))
        {
            throw new InvalidOperationException($"Project file not found at: {projectPath}");
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{projectPath}\" --no-build",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            Environment =
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["ASPNETCORE_URLS"] = ServiceUrl
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

        // Wait for service to be ready - poll for health endpoint
        HttpClient = new HttpClient { BaseAddress = new Uri(ServiceUrl) };

        bool isReady = false;
        for (int i = 0; i < 30 && !_serviceProcess.HasExited; i++)
        {
            DateTime startTime = DateTime.Now;
            try
            {
                HttpResponseMessage response = HttpClient.GetAsync("/health").Result;
                if (response.IsSuccessStatusCode)
                {
                    isReady = true;
                    break;
                }
            }
            catch
            {
                // Service not ready yet
            }

            TimeSpan sleepTime = TimeSpan.FromSeconds(1000) - (DateTime.Now - startTime);
            if (sleepTime > TimeSpan.Zero)
            {
                Thread.Sleep(sleepTime);
            }
        }

        if (!isReady)
        {
            string logs = GetLogs();
            throw new InvalidOperationException($"Service failed to start within 30 seconds. Logs:\n{logs}");
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

        HttpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}
