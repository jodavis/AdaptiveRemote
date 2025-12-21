using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;

namespace AdaptiveRemote.EndtoEndTests.Host;

public class AdaptiveRemoteHost : IDisposable
{
    private readonly AdaptiveRemoteHostSettings _settings;
    private readonly ILogger<AdaptiveRemoteHost> _logger;
    private readonly Lazy<ITestService> _lazyTestService;
    private ITestControlService? _testControlService;

    private readonly StringBuilder _standardOutput = new();
    private readonly StringBuilder _standardError = new();

    private Process? _process = null;
    private TcpClient? _client = null;
    private JsonRpc? _rpc = null;

    public AdaptiveRemoteHost(AdaptiveRemoteHostSettings settings, ILogger<AdaptiveRemoteHost> logger)
    {
        _settings = settings;
        _logger = logger;

        _lazyTestService = new(() =>
        {
            _logger.LogInformation("Creating {TestServiceName} proxy...", nameof(BasicTestService));
            return WaitUtilities.WaitForAsyncTask(
                TestControlService.CreateTestServiceAsync<BasicTestService>,
                _settings.RpcTimeout);
        });
    }

    public ITestService TestService => _lazyTestService.Value;

    public string StandardOutput => _standardOutput.ToString();
    public string StandardError => _standardError.ToString();

    private ITestControlService TestControlService => _testControlService
        ?? throw new InvalidOperationException("You must call Start() before accessing test services");

    public void Start()
    {
        int controlPort = GetAvailablePort();

        AdaptiveRemoteHostSettings settingsWithControlPort = _settings.AddCommandLineArgs($"--test:ControlPort={controlPort}");

        string exePath = Path.GetFullPath(settingsWithControlPort.ExePath);

        ProcessStartInfo startInfo = new()
        {
            FileName = exePath,
            Arguments = settingsWithControlPort.CommandLineArgs,
            WorkingDirectory = _settings.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Apply environment variables from settings
        foreach (KeyValuePair<string, string> kvp in _settings.EnvironmentVariables)
        {
            startInfo.Environment[kvp.Key] = kvp.Value;
        }

        // If DISPLAY is set in parent process but not in settings, inherit it
        // (important for xvfb-run which sets DISPLAY automatically)
        string? displayFromParent = Environment.GetEnvironmentVariable("DISPLAY");
        if (!string.IsNullOrEmpty(displayFromParent) && !_settings.EnvironmentVariables.ContainsKey("DISPLAY"))
        {
            startInfo.Environment["DISPLAY"] = displayFromParent;
        }

        Process process = new()
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data is not null)
            {
                _standardOutput.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data is not null)
            {
                _standardError.AppendLine(e.Data);
            }
        };

        try
        {
            _logger.LogInformation("Starting host process: {ExePath} {Arguments}", startInfo.FileName, startInfo.Arguments);

            _process = process;
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _logger.LogInformation("Host process started with PID: {ProcessId}", _process.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start host process: {ErrorMessage}", ex.Message);
            throw;
        }

        // Wait for the host to be ready and establish control connection
        Exception? connectionError = null;

        _logger.LogInformation("Connecting to test control endpoint on port {Port}...", controlPort);

        WaitUtilities.ExecuteWithRetries(async (cancellationToken) =>
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync("127.0.0.1", controlPort, cancellationToken);

                // Create JsonRpc with target for control methods
                NetworkStream stream = _client.GetStream();
                _rpc = new JsonRpc(stream, stream);
                _rpc.StartListening();

                _logger.LogInformation("Connected to test control endpoint");
                return true;
            }
            catch (Exception ex)
            {
                connectionError = ex;
                _client?.Dispose();
                _client = null;
                return false;
            }

        }, timeout: _settings.StartupTimeout);

        if (_rpc is null)
        {
            _logger.LogError(
                """
                Failed to connect to the test control endpoint on port {ControlPort} within {StartupTimeout}.
                Last error: {ErrorMesssage}
                """,
                controlPort,
                _settings.StartupTimeout,
                connectionError?.Message);
            throw new TimeoutException(
                $"Failed to connect to test control endpoint on port {controlPort} within {_settings.StartupTimeout}. " +
                $"Last error: {connectionError?.Message}");
        }

        // Create control proxy for bootstrapping
        _testControlService = _rpc.Attach<ITestControlService>();
    }

    public void Stop()
    {
        if (_process is not null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _logger.LogInformation("Waiting for host to exit...");
                    _process.WaitForExit(_settings.ShutdownTimeout);
                }
                _logger.LogInformation("Host exited with code: {ExitCode}", _process.ExitCode);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Host did not exit within timeout, killing process");
                try
                {
                    _process.Kill(entireProcessTree: true);
                    // Give the kill signal time to take effect
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error killing process: {ErrorMessage}", ex.Message);
                }
                // For Electron tests, don't fail if shutdown times out but process was killed successfully
                if (!_process.HasExited)
                {
                    throw new TimeoutException($"Host did not exit within {_settings.ShutdownTimeout}");
                }
            }
        }
    }

    private static int GetAvailablePort()
    {
        using TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        try
        {
            _rpc?.Dispose();
        }
        catch { }

        try
        {
            _client?.Dispose();
        }
        catch { }

        try
        {
            if (_process is not null)
            {
                if (!_process.HasExited)
                {
                    _logger.LogWarning("Host process {ProcessId} is still running, killing process", _process.Id);
                    _process.Kill(entireProcessTree: true);
                }
                _process.Dispose();
            }
        }
        catch { }

        GC.SuppressFinalize(this);
    }
}
