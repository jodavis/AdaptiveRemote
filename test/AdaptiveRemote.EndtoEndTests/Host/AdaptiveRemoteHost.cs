using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using AdaptiveRemote.Services.Testing;
using I8Beef.TiVo;
using StreamJsonRpc;

namespace AdaptiveRemote.EndtoEndTests.Host;

public class AdaptiveRemoteHost : IDisposable
{
    private readonly AdaptiveRemoteHostSettings _settings;

    private readonly Lazy<ITestService> _lazyTestService;
    private ITestControlService? _testControlService;

    private readonly StringBuilder _standardOutput = new();
    private readonly StringBuilder _standardError = new();

    private Process? _process = null;
    private TcpClient? _client = null;
    private JsonRpc? _rpc = null;

    internal AdaptiveRemoteHost(AdaptiveRemoteHostSettings settings)
    {
        _settings = settings;

        _lazyTestService = new(() => WaitUtilities.WaitForAsyncTask(
            TestControlService.CreateTestServiceAsync<BasicTestService>,
            _settings.RpcTimeout));
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

        ProcessStartInfo startInfo = new()
        {
            FileName = _settings.ExePath,
            Arguments = settingsWithControlPort.CommandLineArgs,
            WorkingDirectory = _settings.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Apply environment variables from settings
        foreach (var kvp in _settings.EnvironmentVariables)
        {
            startInfo.Environment[kvp.Key] = kvp.Value;
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
                //testContext.WriteLine($"[OUT] {e.Data}");
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data is not null)
            {
                _standardError.AppendLine(e.Data);
                //testContext.WriteLine($"[ERR] {e.Data}");
            }
        };

        //testContext.WriteLine($"Launching host: {startInfo.FileName} {startInfo.Arguments}");
        _process = process;
        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        // Wait for the host to be ready and establish control connection
        Exception? connectionError = null;


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

                //testContext.WriteLine("Connected to test control endpoint");
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
            //context.TestContext.WriteLine("Waiting for host to exit...");

            try
            {
                _process.WaitForExit(_settings.ShutdownTimeout);
                //context.TestContext.WriteLine($"Host exited with code: {context.Process.ExitCode}");
            }
            catch (OperationCanceledException)
            {
                //context.TestContext.WriteLine("Host did not exit within timeout, killing process");
                try
                {
                    _process.Kill(entireProcessTree: true);
                    // Give the kill signal time to take effect
                    Thread.Sleep(1000);
                }
                catch (Exception /*ex*/)
                {
                    //context.TestContext.WriteLine($"Error killing process: {ex.Message}");
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
                if (_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
                _process.Dispose();
            }
        }
        catch { }

        GC.SuppressFinalize(this);
    }
}
