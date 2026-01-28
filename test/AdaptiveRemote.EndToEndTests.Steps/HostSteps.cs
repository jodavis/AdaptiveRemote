using AdaptiveRemote.EndtoEndTests;
using AdaptiveRemote.EndtoEndTests.Host;
using AdaptiveRemote.EndtoEndTests.Logging;
using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class HostSteps : StepsBase
{
    private const string TiVoDeviceName = "TiVo";
    private string LogFilePath => Path.Combine(TestContext.TestResultsDirectory!, TestContext.TestName + ".log");

    [BeforeScenario(Order = 50)]
    public void OnBeforeScenario_SetUpSimulatedEnvironment()
    {
        SimulatedEnvironment simulatedEnvironment = new SimulatedEnvironment();
        ProvideContainerObject<ISimulatedEnvironment>(simulatedEnvironment);

        // Register and start the simulated TiVo device for all tests
        // Create a simple logger that writes to TestContext
        ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new TestContextLoggerProvider(TestContext));
        });
        ILogger logger = loggerFactory.CreateLogger("SimulatedTiVoDevice");

        ISimulatedDeviceBuilder builder = new SimulatedTiVoDeviceBuilder(logger);
        simulatedEnvironment.RegisterDevice(TiVoDeviceName, builder);
        ISimulatedDevice device = simulatedEnvironment.StartDevice(TiVoDeviceName);

        TestContext.WriteLine($"Simulated TiVo device started on port {device.Port}");
    }

    [BeforeScenario(Order = 200)]
    public void OnBeforeScenario_SetUpHostFactory(AdaptiveRemoteHostSettings hostSettings)
    {
        if (!File.Exists(hostSettings.ExePath))
        {
            Assert.Inconclusive($"Host not found at: {hostSettings.ExePath}");
        }

        if (!Directory.Exists(hostSettings.WorkingDirectory))
        {
            Assert.Inconclusive($"Working directory not found: {hostSettings.WorkingDirectory}");
        }

        // Use the simulated TiVo device
        string tivoArgs = "--tivo:Fake=True";
        if (SimulatedEnvironment.TryGetDevice(TiVoDeviceName, out ISimulatedDevice? tivoDevice) && tivoDevice != null)
        {
            tivoArgs = $"--tivo:IP=127.0.0.1:{tivoDevice.Port}";
        }

        hostSettings = hostSettings.AddCommandLineArgs($"{tivoArgs} --broadlink:Fake=True --log:FilePath=\"{LogFilePath}\"");

        ProvideContainerObjectFactory(() => AdaptiveRemoteHost.CreateBuilder(hostSettings)
            .ConfigureLogging(builder =>
            {
                builder.AddDebug();
                builder.AddTestContext(TestContext);
            })
            .Start());
    }

    [Given(@"the application is running")]
    public void GivenTheApplicationIsRunning()
    {
        _ = Host; // Trigger lazy initialization
    }

    [Given(@"the application is not running")]
    public void GivenTheApplicationIsNotRunning()
    {
        if (IsHostRunning)
        {
            Host.Stop();
        }
    }

    [Given(@"the application is in the Ready state")]
    public void GivenTheApplicationIsInTheReadyState()
    {
        if (!IsHostRunning)
        {
            Assert.Fail("Cannot check application state. The application is not started.");
        }

        // Wait for the application to be in Ready state
        Host.Application.WaitForPhase(LifecyclePhase.Ready, timeout: TimeSpan.FromSeconds(60));
    }

    [When(@"I start the application")]
    public void WhenIStartTheApplication()
    {
        Assert.IsFalse(IsHostRunning, "Cannot start the application because it is already running.");
        _ = Host; // Trigger lazy initialization
    }

    [When(@"I wait for the application to shut down")]
    public void WhenIWaitForTheApplicationToShutDown()
    {
        Assert.IsNotNull(Host, "Cannot wait for the application to shut down. The application is not started.");
        Host.WaitForShutdown();
    }

    [Then(@"I should see the application in the {LifecyclePhase} phase")]
    public void ThenIShouldSeeTheApplicationInAGivenState(LifecyclePhase phase)
    {
        Assert.IsNotNull(Host, "Cannot check the application state. The application is not started.");
        Host.Application.WaitForPhase(phase, timeout: TimeSpan.FromSeconds(60));
    }

    [AfterScenario]
    public void OnAfterScenario_AttachLogsToTestContextAndStopHost()
    {
        if (File.Exists(LogFilePath))
        {
            TestContext.AddResultFile(LogFilePath);
            TestContext.WriteLine("Log file found and attached");
        }
        else
        {
            TestContext.WriteLine("No log file found at {0}", LogFilePath);
        }

        if (IsHostRunning)
        {
            Host.Stop();
        }

        // Clean up simulated environment
        SimulatedEnvironment?.Dispose();
    }
}
