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

        string projectPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..",
            "src", "AdaptiveRemote.Backend.CompiledLayoutService",
            "AdaptiveRemote.Backend.CompiledLayoutService.csproj");

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

        // Wait for service to be ready
        Thread.Sleep(3000);

        HttpClient = new HttpClient { BaseAddress = new Uri(ServiceUrl) };
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
