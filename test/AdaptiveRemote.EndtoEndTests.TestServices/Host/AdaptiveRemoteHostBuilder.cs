using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using StreamJsonRpc;

namespace AdaptiveRemote.EndtoEndTests.Host;

public class AdaptiveRemoteHostBuilder
{
    private readonly AdaptiveRemoteHostSettings _settings;
    private readonly List<Action<ILoggingBuilder>> _configureLogging = new();

    public AdaptiveRemoteHostBuilder(AdaptiveRemoteHostSettings settings)
    {
        _settings = settings;
    }

    public AdaptiveRemoteHostBuilder ConfigureLogging(Action<ILoggingBuilder> configureLogging)
    {
        _configureLogging.Add(configureLogging);
        return this;
    }

    public AdaptiveRemoteHost Start()
    {
        ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            foreach (Action<ILoggingBuilder> configure in _configureLogging)
            {
                configure(builder);
            }
        });
        ILogger<AdaptiveRemoteHost> logger = loggerFactory.CreateLogger<AdaptiveRemoteHost>();

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

        StringBuilder standardOutput = new();
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data is not null)
            {
                standardOutput.AppendLine(e.Data);
            }
        };

        StringBuilder standardError = new();
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data is not null)
            {
                standardError.AppendLine(e.Data);
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

        TcpClient? client = null;
        JsonRpc? rpc = null;
        try
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for the host to be ready and establish control connection
            Exception? connectionError = null;

            logger.LogInformation("Connecting to test control endpoint on port {Port}...", controlPort);

            WaitUtilities.ExecuteWithRetries(async (cancellationToken) =>
            {
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
            ITestControlService testControlService = rpc.Attach<ITestControlService>();

            return new(_settings, loggerFactory, logger, process, client, rpc, testControlService, standardOutput, standardError);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start the host. {ErrorMessage}", ex.Message);

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

            throw;
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

}
