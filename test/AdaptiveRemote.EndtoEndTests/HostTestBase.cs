using AdaptiveRemote.EndtoEndTests.Host;
using AdaptiveRemote.EndtoEndTests.Logging;
using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// Base class for E2E tests that launch and control host applications.
/// </summary>
public abstract class HostTestBase
{
    /// <summary>
    /// Finds the solution root directory by looking for the .sln file.
    /// </summary>
    protected static string GetSolutionRoot()
    {
        string baseDir = AppContext.BaseDirectory;
        DirectoryInfo? dir = new DirectoryInfo(baseDir);

        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0)
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new InvalidOperationException($"Could not find solution root starting from {baseDir}");
    }

    /// <summary>
    /// Gets configuration settings to start up the host
    /// </summary>
    protected abstract AdaptiveRemoteHostSettings GetHostSettings(string solutionRoot);

    /// <summary>
    /// Runs a standard E2E test: launch host, load test service, execute test, and shutdown.
    /// </summary>
    protected void RunStandardE2ETestAsync(
        string solutionRoot,
        TestContext testContext)
    {
        AdaptiveRemoteHostSettings hostSettings = GetHostSettings(solutionRoot);

        if (!File.Exists(hostSettings.ExePath))
        {
            Assert.Inconclusive($"Host not found at: {hostSettings.ExePath}");
        }

        if (!Directory.Exists(hostSettings.WorkingDirectory))
        {
            Assert.Inconclusive($"Working directory not found: {hostSettings.WorkingDirectory}");
        }

        string logFilePath = Path.Combine(testContext.TestResultsDirectory!, testContext.TestName + ".log");

        hostSettings = hostSettings.AddCommandLineArgs($"--tivo:Fake=True --broadlink:Fake=True --log:FilePath=\"{logFilePath}\"");

        using AdaptiveRemoteHost host = AdaptiveRemoteHost.CreateBuilder(hostSettings)
            .ConfigureLogging(builder =>
            {
                builder.AddDebug();
                builder.AddTestContext(testContext);
            })
            .Start();

        ILogger logger = CreateTypedLogger(host);

        try
        {
            // Load test service
            IApplicationTestService testService = host.Application;

            using (logger.BeginScope("Executing test"))
            {
                logger.LogInformation("Waiting for application to reach Ready phase...");
                // Wait for application ready - this ensures the UI has rendered and the application scope exists
                testService.WaitForPhase(LifecyclePhase.Ready, TimeSpan.FromSeconds(60));

                logger.LogInformation("Invoking Exit command...");
                // Request shutdown via strongly-typed proxy
                host.UI.ClickButton("Exit", TimeSpan.FromSeconds(30));
            }

            // Wait for shutdown
            host.Stop();

            if (File.Exists(logFilePath))
            {
                logger.LogInformation("Found log file at {LogFilePath}", logFilePath);
                testContext.AddResultFile(logFilePath);
            }
            else
            {
                logger.LogWarning("No log file found at {LogFilePath}", logFilePath);
            }

            // Verify logs (optional, can be overridden)
            VerifyLogs(host, logger);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex,
                """
                Test failed with exception: {ErrorMessage}
                === Standard Output ===
                {StandardOutput}
                === Standard Error ===
                {StandardError}
                """,
                ex.Message,
                host.StandardOutput,
                host.StandardError);
            throw;
        }
    }

    protected abstract ILogger CreateTypedLogger(AdaptiveRemoteHost host);

    protected virtual void VerifyLogs(AdaptiveRemoteHost host, ILogger logger)
    {
        string logs = host.StandardOutput;
        string errors = host.StandardError;

        // Check for error/warning patterns
        bool hasErrors = logs.Contains("err:", StringComparison.OrdinalIgnoreCase) ||
                        errors.Contains("err:", StringComparison.OrdinalIgnoreCase);

        bool hasWarnings = logs.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
                          errors.Contains("warning", StringComparison.OrdinalIgnoreCase);

        if (hasErrors)
        {
            Assert.Fail($"Host logs contain errors:\n{logs}\n\nStderr:\n{errors}");
        }

        // Note: Warnings are informational but not a failure for now
        if (hasWarnings)
        {
            logger.LogWarning("Host logs contain warnings");
        }
    }
}
