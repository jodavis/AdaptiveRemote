using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Logging;
using StreamJsonRpc;

namespace AdaptiveRemote.EndtoEndTests.Host;

public partial class AdaptiveRemoteHost : IDisposable
{
    private readonly AdaptiveRemoteHostSettings _settings;
    private readonly ILogger<AdaptiveRemoteHost> _logger;
    private readonly Lazy<ITestService> _lazyTestService;
    private readonly ITestControlService _testControlService;

    private readonly StringBuilder _standardOutput;
    private readonly StringBuilder _standardError;

    private readonly Process _process;
    private readonly TcpClient _client;
    private readonly JsonRpc _rpc;

    private AdaptiveRemoteHost(AdaptiveRemoteHostSettings settings,
                               ILoggerFactory loggerFactory,
                               ILogger<AdaptiveRemoteHost> logger,
                               Process process,
                               TcpClient client,
                               JsonRpc rpc,
                               ITestControlService testControlService,
                               StringBuilder standardOutput,
                               StringBuilder standardError)
    {
        _settings = settings;
        LoggerFactory = loggerFactory;
        _logger = logger;
        _process = process;
        _client = client;
        _rpc = rpc;
        _testControlService = testControlService;
        _standardOutput = standardOutput;
        _standardError = standardError;

        _lazyTestService = new(() =>
        {
            _logger.LogInformation("Creating {TestServiceName} proxy...", nameof(BasicTestService));
            return WaitUtilities.WaitForAsyncTask(
                _testControlService.CreateTestServiceAsync<BasicTestService>,
                _settings.RpcTimeout);
        });
    }

    public ITestService TestService => _lazyTestService.Value;

    public ILoggerFactory LoggerFactory { get; }

    public string StandardOutput => _standardOutput.ToString();
    public string StandardError => _standardError.ToString();

    public void Stop()
    {
        if (_process is not null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _logger.LogInformation("Waiting for host to exit...");
                    bool exited = _process.WaitForExit(_settings.ShutdownTimeout);
                    if (!exited)
                    {
                        _logger.LogWarning("Host did not exit within timeout of {ShutdownTimeout}", _settings.ShutdownTimeout);
                    }
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

    public void Dispose()
    {
        try
        {
            _rpc.Dispose();
        }
        catch { }

        try
        {
            _client.Dispose();
        }
        catch { }

        try
        {
            if (!_process.HasExited)
            {
                _logger.LogWarning("Host process {ProcessId} is still running, killing process", _process.Id);
                _process.Kill(entireProcessTree: true);
            }
            _process.Dispose();
        }
        catch { }

        GC.SuppressFinalize(this);
    }
}
