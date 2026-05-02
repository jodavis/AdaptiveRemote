using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using AdaptiveRemote.EndtoEndTests.Logging;
using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StreamJsonRpc;

namespace AdaptiveRemote.EndtoEndTests.Host;

public partial class AdaptiveRemoteHost
{
    public class Builder
    {
        private const string ResourceInUseExceptionMessage = "The requested resource is in use.";
        private const int StartupRetryCount = 3;

        private AdaptiveRemoteHostSettings _settings;
        private readonly ILoggerFactory _loggerFactory;
        private readonly HostApplicationLoggerProvider _hostLoggerProvider;
        private readonly List<Action<ILoggingBuilder>> _configureLogging = new();
        private readonly List<Func<ITestEndpoint, CancellationToken, Task>> _configureServices = new();

        internal Builder(AdaptiveRemoteHostSettings settings, ILoggerFactory loggerFactory, HostApplicationLoggerProvider hostLoggerProvider)
        {
            _settings = settings;
            _loggerFactory = loggerFactory;
            _hostLoggerProvider = hostLoggerProvider;
        }

        public Builder ConfigureSettings(Func<AdaptiveRemoteHostSettings, AdaptiveRemoteHostSettings> configureSettings)
        {
            _settings = configureSettings(_settings);
            return this;
        }

        /// <summary>
        /// Registers a callback to inject test services before the host is built.
        /// The callback is invoked after connecting to the test endpoint but before BuildAndRunHostAsync is called.
        /// </summary>
        public Builder ConfigureTestServices(Func<ITestEndpoint, CancellationToken, Task> configureServices)
        {
            _configureServices.Add(configureServices);
            return this;
        }

        public AdaptiveRemoteHost Start(Func<AdaptiveRemoteHostSettings, AdaptiveRemoteHostSettings>? overrideSettings = null)
        {
            // Add a small random delay to reduce the chance of resource conflicts when starting multiple hosts in parallel
            Thread.Sleep(Random.Shared.Next(100, 500)); 

            AdaptiveRemoteHostSettings effectiveSettings = overrideSettings?.Invoke(_settings) ?? _settings;

            ILogger<AdaptiveRemoteHost> retryLogger = _loggerFactory.CreateLogger<AdaptiveRemoteHost>();

            // Only HostStartupException instances whose CapturedOutput contains ResourceInUseExceptionMessage
            // are retried. Any other exception (e.g. Inconclusive, process-launch failure) propagates immediately.
            for (int attempt = 1; attempt <= StartupRetryCount; attempt++)
            {
                try
                {
                    return StartWithSettings(effectiveSettings);
                }
                catch (HostStartupException ex) when (ex.CapturedOutput.Contains(ResourceInUseExceptionMessage, StringComparison.OrdinalIgnoreCase))
                {
                    if (attempt == StartupRetryCount)
                    {
                        throw;
                    }

                    retryLogger.LogWarning(
                        "Host startup attempt {Attempt}/{MaxAttempts} failed with '{ExceptionMessage}'. Retrying...",
                        attempt, StartupRetryCount, ResourceInUseExceptionMessage);
                    Thread.Sleep(Random.Shared.Next(1000, 3000));
                }
            }

            // Unreachable: the loop always exits via return (success) or throw (final-attempt failure
            // re-throws via the catch block, or a non-retryable exception propagates immediately).
            // Required by the compiler since it cannot prove the loop body always returns or throws.
            throw new InvalidOperationException("Unexpected state: startup retry loop exited without returning or throwing.");
        }

        private AdaptiveRemoteHost StartWithSettings(AdaptiveRemoteHostSettings _settings)
        {
            if (!File.Exists(_settings.ExePath))
            {
                Assert.Inconclusive($"Host not found at: {_settings.ExePath}");
            }

            if (!Directory.Exists(_settings.WorkingDirectory))
            {
                Assert.Inconclusive($"Working directory not found: {_settings.WorkingDirectory}");
            }

            ILogger<AdaptiveRemoteHost> logger = _loggerFactory.CreateLogger<AdaptiveRemoteHost>();

            // Configure the test control port
            int controlPort = GetAvailablePort();

            AdaptiveRemoteHostSettings settingsWithControlPort = _settings.AddCommandLineArgs($"--test:ControlPort={controlPort}");

            // Configure the WebView debugging
            settingsWithControlPort = settingsWithControlPort.AddCommandLineArgs($"--test:WebViewDebugging=True");

            // Start the host process
            string exePath = Path.GetFullPath(settingsWithControlPort.ExePath);

            ProcessStartInfo startInfo = new()
            {
                FileName = exePath,
                Arguments = settingsWithControlPort.CommandLineArgs,
                WorkingDirectory = _settings.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            foreach (KeyValuePair<string, string> kvp in _settings.EnvironmentVariables)
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }

            Process process = new()
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            StringBuilder standardOutputAndError = new();
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data is not null)
                {
                    standardOutputAndError.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data is not null)
                {
                    standardOutputAndError.AppendLine(e.Data);
                }
            };

            process.Exited += (sender, e) =>
            {
                if (process.ExitCode != 0)
                {
                    logger.LogWarning("Host process exited with code {ExitCode}. Standard output and error:\n{OutputAndError}",
                        process.ExitCode, standardOutputAndError.ToString());
                }
            };

            try
            {
                logger.LogInformation("Starting host process: {ExePath} {Arguments}", startInfo.FileName, startInfo.Arguments);

                process.Start();

                logger.LogInformation("Host process started with PID: {ProcessId}", process.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start host process: {ErrorMessage}", ex.Message);
                throw;
            }

            // Connect to the test control endpoint
            TcpClient? client = null;
            JsonRpc? rpc = null;
            try
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for the host to be ready and establish control connection
                Exception? connectionError = null;

                logger.LogInformation("Connecting to test control endpoint on port {Port}...", controlPort);

                WaitHelpers.ExecuteWithRetries(async (cancellationToken) =>
                {
                    if (process.HasExited)
                    {
                        throw new InvalidOperationException(
                            $"Host process (PID {process.Id}) exited with exit code {process.ExitCode} before the test control endpoint became available.");
                    }

                    try
                    {
                        client = new TcpClient();
                        await client.ConnectAsync("127.0.0.1", controlPort, cancellationToken);

                        // Create JsonRpc with target for control methods
                        NetworkStream stream = client.GetStream();
                        rpc = new JsonRpc(stream, stream);
                        rpc.StartListening();

                        logger.LogInformation("Connected to test control endpoint");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        connectionError = ex;
                        client?.Dispose();
                        client = null;
                        return false;
                    }

                }, timeout: _settings.StartupTimeout);

                if (client is null || rpc is null)
                {
                    logger.LogError(
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
                ITestEndpoint testEndpoint = rpc.Attach<ITestEndpoint>();

                // Allow test configuration to inject services before building
                if (_configureServices.Count > 0)
                {
                    logger.LogInformation("Injecting test services...");
                    foreach (Func<ITestEndpoint, CancellationToken, Task> configureService in _configureServices)
                    {
                        WaitHelpers.WaitForAsyncTask(ct => configureService(testEndpoint, ct), _settings.StartupTimeout);
                    }
                    logger.LogInformation("Test services injected");
                }

                // Signal the host to build and run
                logger.LogInformation("Signaling host to build and run");
                WaitHelpers.WaitForAsyncTask(testEndpoint.BuildAndRunHostAsync, _settings.StartupTimeout);

                // Get the test service provider
                logger.LogInformation("Getting test service provider");
                ITestServiceProvider serviceProvider = WaitHelpers.WaitForAsyncTask(testEndpoint.GetTestServiceProviderAsync, _settings.StartupTimeout);

                // Attach the RPC proxy to our HostRpcLoggerProvider so test-side logs are forwarded to the host
                ITestLogger testLogger = serviceProvider.CreateTestService<ITestLogger, HostApplicationTestLogger>(_settings.StartupTimeout);
                _hostLoggerProvider.AttachTestLoggerProxy(testLogger);
                logger.LogInformation("Attached RPC test logger");

                return new(_settings, _loggerFactory, process, client, rpc, testEndpoint, standardOutputAndError);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start the host. {ErrorMessage}", ex.Message);

                string capturedOutput = standardOutputAndError.ToString();
                if (!string.IsNullOrWhiteSpace(capturedOutput))
                {
                    logger.LogError("Host process output:\n{Output}", capturedOutput);
                }

                try
                {
                    rpc?.Dispose();
                }
                catch (Exception rpcException)
                {
                    logger.LogError(rpcException, "Failed to dispose the RPC connection. {ErrorMessage}", rpcException.Message);
                }

                try
                {
                    client?.Dispose();
                }
                catch (Exception tcpException)
                {
                    logger.LogError(tcpException, "Failed to dispose the TCP connection. {ErrorMessage}", tcpException.Message);
                }

                try
                {
                    if (!process.HasExited)
                    {
                        logger.LogWarning("Host process {ProcessId} is still running, killing process", process.Id);
                        process.Kill(entireProcessTree: true);
                    }
                    process.Dispose();
                }
                catch (Exception processException)
                {
                    logger.LogError(processException, "Failed to kill the host process. {ErrorMessage}", processException.Message);
                }

                throw new HostStartupException(ex.Message, ex, standardOutputAndError.ToString());
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

        private sealed class HostStartupException(string message, Exception innerException, string capturedOutput)
            : Exception(message, innerException)
        {
            /// <summary>
            /// The captured stdout/stderr from the host process at the time of the startup failure.
            /// Used by <see cref="Start"/> to identify retryable crashes (e.g., WebView2 resource-in-use).
            /// </summary>
            internal string CapturedOutput { get; } = capturedOutput;
        }
    }
}

