using System.Collections.Immutable;
using AdaptiveRemote.EndtoEndTests.Host;
using AdaptiveRemote.EndtoEndTests.Logging;
using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.EndtoEndTests;

[TestClass]
public class HeadlessHostTests : HostTestBase
{
    private static string? _solutionRoot;

    private static readonly ImmutableDictionary<string, string> StandardHeadlessEnvironmentVariables =
        ImmutableDictionary<string, string>.Empty;

    public TestContext TestContext { get; set; } = null!;

    public string? TracesPath => Path.Combine(TestContext.TestResultsDirectory!, "playwright-traces");

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        _solutionRoot = GetSolutionRoot();

        context.WriteLine($"Solution root: {_solutionRoot}");
    }

    protected override AdaptiveRemoteHostSettings GetHostSettings(string solutionRoot)
    {
        // On Linux, the executable doesn't have .exe extension
        string exeName = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
            ? "AdaptiveRemote.Headless.exe"
            : "AdaptiveRemote.Headless";
        
        return new(
            ExePath: Path.Combine(solutionRoot, $"src/AdaptiveRemote.Headless/bin/Debug/net8.0/{exeName}"),
            CommandLineArgs: $"--playwright:TracesDir=\"{TracesPath}\"",
            EnvironmentVariables: StandardHeadlessEnvironmentVariables);
    }

    protected override ILogger CreateTypedLogger(AdaptiveRemoteHost host) 
        => host.CreateLogger<HeadlessHostTests>();

    [TestMethod]
    [Timeout(180000)] // 3 minutes
    public void HeadlessHost_StartsAndRespondsToTestCommands()
    {
        RunStandardE2ETestAsync(_solutionRoot!, TestContext);

        foreach (string traceFile in Directory.GetFiles(TracesPath!, "*.zip"))
        {
            TestContext.AddResultFile(traceFile);
        }
    }

    [TestMethod]
    [Timeout(180000)] // 3 minutes
    public void HeadlessHost_UIInteraction_ExitButtonClickable()
    {
        RunUITestAsync(_solutionRoot!, TestContext);

        foreach (string traceFile in Directory.GetFiles(TracesPath!, "*.zip"))
        {
            TestContext.AddResultFile(traceFile);
        }
    }

    private void RunUITestAsync(string solutionRoot, TestContext testContext)
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
            // Load test services
            IApplicationTestService testService = host.Application;
            IUITestService uiTestService = host.UI;

            using (logger.BeginScope("Executing UI test"))
            {
                logger.LogInformation("Waiting for application to reach Ready phase...");
                // Wait for application ready - this ensures the UI has rendered
                testService.WaitForPhase(LifecyclePhase.Ready, TimeSpan.FromSeconds(60));

                logger.LogInformation("Checking if Exit button is visible...");
                bool isVisible = uiTestService.IsButtonVisible("Exit");
                Assert.IsTrue(isVisible, "Exit button should be visible");

                logger.LogInformation("Checking if Exit button is enabled...");
                bool isEnabled = uiTestService.IsButtonEnabled("Exit");
                Assert.IsTrue(isEnabled, "Exit button should be enabled");

                logger.LogInformation("Clicking Exit button...");
                uiTestService.ClickButton("Exit");
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

            // Verify logs
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
}
