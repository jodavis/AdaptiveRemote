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
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<AdaptiveRemoteHost> _logger;
    private readonly Lazy<IApplicationTestService> _lazyTestService;
    private readonly Lazy<IUITestService> _lazyUITestService;
    private readonly Lazy<ITestSpeechRecognitionService> _lazySpeechTestService;
    private readonly ITestEndpoint _testEndpoint;

    private readonly StringBuilder _standardOutput;
    private readonly StringBuilder _standardError;

    private readonly Process _process;
    private readonly TcpClient _client;
    private readonly JsonRpc _rpc;

    private AdaptiveRemoteHost(AdaptiveRemoteHostSettings settings,
                               ILoggerFactory loggerFactory,
                               Process process,
                               TcpClient client,
                               JsonRpc rpc,
                               ITestEndpoint testEndpoint,
                               StringBuilder standardOutput,
                               StringBuilder standardError)
    {
        _settings = settings;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<AdaptiveRemoteHost>();
        _process = process;
        _client = client;
        _rpc = rpc;
        _testEndpoint = testEndpoint;
        _standardOutput = standardOutput;
        _standardError = standardError;

        _lazyTestService = CreateLazyTestService<ApplicationTestService, IApplicationTestService>();

        // Choose UI test service based on settings
        _lazyUITestService = _settings.UIService switch
        {
            UIServiceType.Playwright => CreateLazyTestService<PlaywrightUITestService, IUITestService>(),
            UIServiceType.BlazorWebView => CreateLazyTestService<BlazorWebViewUITestService, IUITestService>(),
            _ => throw new InvalidOperationException($"Unsupported UIServiceType '{_settings.UIService}'")
        };

        // Create speech test service
        _lazySpeechTestService = CreateLazyTestService<TestSpeechRecognitionService, ITestSpeechRecognitionService>();
    }

    private Lazy<TInterface> CreateLazyTestService<TImplementation, TInterface>()
        where TImplementation : TInterface
    {
        return new Lazy<TInterface>(() =>
        {
            using (_logger.LogTimedOperation($"Creating {typeof(TImplementation).Name} proxy"))
            {
                try
                {
                    return WaitHelpers.WaitForAsyncTask(ct => _testEndpoint.CreateTestServiceAsync<TInterface, TImplementation>(ct), _settings.RpcTimeout);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create {TestServiceName} proxy", typeof(TImplementation).Name);
                    throw;
                }
            }
        });
    }

    public IApplicationTestService Application => _lazyTestService.Value;

    public IUITestService UI => _lazyUITestService.Value;

    public ITestSpeechRecognitionService Speech => _lazySpeechTestService.Value;

    public ILogger CreateLogger<CategoryType>() => _loggerFactory.CreateLogger<CategoryType>();

    public ILogger CreateLogger(string category) => _loggerFactory.CreateLogger(category);

    public string StandardOutput => _standardOutput.ToString();
    public string StandardError => _standardError.ToString();

    public bool IsRunning => !_process.HasExited;

    public bool WaitForShutdown()
    {
        return _process.WaitForExit(_settings.ShutdownTimeout);
    }

    public void Stop()
    {
        if (_process is not null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    // Try sending a shutdown command
                    try
                    {
                        _logger.LogInformation("Sending shutdown command...");
                        Application.StopApplication(_settings.RpcTimeout);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failure from shutdown command");
                    }

                    _logger.LogInformation("Waiting for host to exit...");
                    bool exited = _process.WaitForExit(_settings.ShutdownTimeout);
                    if (!exited)
                    {
                        _logger.LogWarning("Host did not exit within timeout of {ShutdownTimeout}", _settings.ShutdownTimeout);
                        throw new OperationCanceledException();
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
