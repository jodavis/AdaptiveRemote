using AdaptiveRemote.EndtoEndTests.Host;
using AdaptiveRemote.Services.Testing;

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

        hostSettings = hostSettings.AddCommandLineArgs("--tivo:Fake=True --broadlink:Fake=True");

        using AdaptiveRemoteHost host = new(hostSettings);

        try
        {
            testContext.WriteLine($"Starting host: {hostSettings.ExePath}");
            host.Start();

            testContext.WriteLine("Getting test service...");
            // Load test service
            ITestService testService = host.TestService;

            testContext.WriteLine("Waiting for application to reach Ready phase...");
            // Wait for application ready - this ensures the UI has rendered and the application scope exists
            testService.WaitForPhase(LifecyclePhase.Ready, TimeSpan.FromSeconds(60));

            testContext.WriteLine("Invoking Exit command...");
            // Request shutdown via strongly-typed proxy
            testService.InvokeCommand("Exit");

            testContext.WriteLine("Stopping host...");
            // Wait for shutdown
            host.Stop();

            // Verify logs (optional, can be overridden)
            VerifyLogs(host, testContext);
        }
        catch (Exception ex)
        {
            testContext.WriteLine($"Test failed with exception: {ex.Message}");
            testContext.WriteLine($"=== Standard Output ===");
            testContext.WriteLine(host.StandardOutput);
            testContext.WriteLine($"=== Standard Error ===");
            testContext.WriteLine(host.StandardError);
            throw;
        }
    }

    protected virtual void VerifyLogs(AdaptiveRemoteHost host, TestContext testContext)
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
            testContext.WriteLine("WARNING: Host logs contain warnings");
        }
    }
}
