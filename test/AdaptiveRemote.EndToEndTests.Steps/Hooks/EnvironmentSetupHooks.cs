using AdaptiveRemote.EndtoEndTests.Host;
using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;
using Reqnroll.BoDi;
using System.Reflection;

namespace AdaptiveRemote.EndToEndTests.Steps.Hooks;

[Binding]
internal class EnvironmentSetupHooks
{
    private static SimulatedEnvironment? _startedEnvironment;
    private static Lazy<ILogger>? _lazyLogger;

    [BeforeTestRun]
    public static void OnBeforeTestRun_SetUpEnvironment(IObjectContainer container, ILoggerFactory loggerFactory)
    {
        _lazyLogger = new Lazy<ILogger>(() => loggerFactory.CreateLogger<EnvironmentSetupHooks>());

        _startedEnvironment ??= container.Resolve<SimulatedEnvironment>();
        container.RegisterInstanceAs<ISimulatedEnvironment>(_startedEnvironment);

        // Configure cloud asset paths once for the entire test run.
        string assemblyDir = Path.GetDirectoryName(typeof(EnvironmentSetupHooks).Assembly.Location)!;
        string testRunTempDir = Path.Combine(assemblyDir, "cloud-test-run");
        string cachePath = Path.Combine(testRunTempDir, "cache");
        string stubFilePath = Path.Combine(testRunTempDir, "stub.json");

        Directory.CreateDirectory(testRunTempDir);
        // Write the full layout so non-cloud scenarios see the complete button set.
        string updatedLayout = File.ReadAllText(Path.Combine(assemblyDir, "Layout", "updated-layout.json"));
        File.WriteAllText(stubFilePath, updatedLayout);

        _startedEnvironment.SetCloudAssetPaths(cachePath, stubFilePath);
    }

    [AfterTestRun]
    public static void OnAfterTestRun_CleanupEnvironment()
    {
        try
        {
            _startedEnvironment?.Dispose();
        }
        catch (Exception ex)
        {
            _lazyLogger?.Value.LogError(ex, "Error cleaning up environment");
        }
    }

    [BeforeScenario]
    public static void OnBeforeScenario_SetLogLocation(IObjectContainer container, TestContext testContext)
    {
        string logLocation = Path.Combine(testContext.TestResultsDirectory!, $"AdaptiveRemote{DateTime.Now:HHmmssfff}.log");

        _startedEnvironment ??= container.Resolve<SimulatedEnvironment>();
        _startedEnvironment.SetLogLocation(logLocation);
    }

    [BeforeScenario]
    public static void OnBeforeScenario_ClearBroadlinkPackets(IObjectContainer container)
    {
        _startedEnvironment ??= container.Resolve<SimulatedEnvironment>();
        _startedEnvironment.Broadlink.ClearRecordedPackets();
        _startedEnvironment.SetCloudAuthCredentials(clientId: null, clientSecret: null);
    }

    [AfterScenario]
    public static void OnAfterScenario_AttachLogsToTestResult(TestContext testContext)
    {
        string? logLocation = _startedEnvironment?.HostLogs;

        if (logLocation is null)
        {
            testContext.WriteLine("No log location had been set for the host.");
            return;
        }

        if (File.Exists(logLocation))
        {
            testContext.AddResultFile(logLocation);
            testContext.WriteLine("Log file found and attached");
        }
        else
        {
            testContext.WriteLine("Log file not found at expected location: " + logLocation);
        }
    }

    [AfterScenario(Order = 1000000)]
    public static void OnAfterScenario_StopHostIfTestFailed(ScenarioContext scenario)
    {
        if (scenario.ScenarioExecutionStatus != ScenarioExecutionStatus.OK && _startedEnvironment is not null)
        {
            _lazyLogger?.Value.LogWarning("Scenario ended with status {ScenarioExecutionStatus}. The host will be restarted.", scenario.ScenarioExecutionStatus);
            _startedEnvironment.StopHostIfRunning();
        }
    }
}
