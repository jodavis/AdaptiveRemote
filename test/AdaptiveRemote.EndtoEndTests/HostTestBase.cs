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

        host.Start();

        // Load test service
        ITestService testService = host.TestService;

        // Wait for application ready
        testService.WaitForPhase(LifecyclePhase.Ready, TimeSpan.FromSeconds(30));

        // Request shutdown via strongly-typed proxy
        testService.InvokeCommand("Exit");

        // Wait for shutdown
        host.Stop();

        // Verify logs (optional, can be overridden)
        VerifyLogs(host, testContext);
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
