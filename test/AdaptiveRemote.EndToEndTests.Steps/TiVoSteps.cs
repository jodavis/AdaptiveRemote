using AdaptiveRemote.EndtoEndTests;
using AdaptiveRemote.EndtoEndTests.Logging;
using AdaptiveRemote.EndtoEndTests.SimulatedTiVo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class TiVoSteps : StepsBase
{
    private const string TiVoDeviceName = "TiVo";

    [BeforeScenario("@tivo", Order = 100)]
    public void OnBeforeScenario_StartSimulatedTiVoDevice()
    {
        ITestEnvironment testEnvironment = GetContainerObject<ITestEnvironment>();
        Microsoft.Extensions.Logging.ILogger logger = TestContext.GetLogger("SimulatedTiVoDevice");

        ITestDeviceBuilder builder = new SimulatedTiVoDeviceBuilder(logger);
        testEnvironment.RegisterDevice(TiVoDeviceName, builder);

        ITestDevice device = testEnvironment.StartDevice(TiVoDeviceName);

        TestContext.WriteLine($"Simulated TiVo device started on port {device.Port}");
    }

    [Given(@"there is a simulated TiVo device")]
    public void GivenThereIsASimulatedTiVoDevice()
    {
        ITestEnvironment testEnvironment = GetContainerObject<ITestEnvironment>();

        if (!testEnvironment.TryGetDevice(TiVoDeviceName, out ITestDevice? _))
        {
            Assert.Fail("Simulated TiVo device is not running. Ensure the test scenario is tagged with @tivo.");
        }

        TestContext.WriteLine("Simulated TiVo device is running");
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

    [Then(@"I should see the TiVo receives a {string} message")]
    public void ThenIShouldSeeTheTiVoReceivesAMessage(string expectedCommand)
    {
        ITestEnvironment testEnvironment = GetContainerObject<ITestEnvironment>();

        if (!testEnvironment.TryGetDevice(TiVoDeviceName, out ITestDevice? device))
        {
            Assert.Fail("TiVo device is not running");
        }

        // Poll for the message with a timeout of 3 seconds
        bool found = WaitHelpers.ExecuteWithRetries(
            () =>
            {
                IReadOnlyList<RecordedMessage> messages = device!.GetRecordedMessages();
                return messages.Any(m => m.Incoming && m.Payload.Contains(expectedCommand, StringComparison.OrdinalIgnoreCase));
            },
            timeoutInSeconds: 3);

        if (!found)
        {
            IReadOnlyList<RecordedMessage> messages = device!.GetRecordedMessages();
            string recordedMessages = string.Join(", ", messages.Select(m => $"[{m.Timestamp:HH:mm:ss.fff}] {(m.Incoming ? "←" : "→")} {m.Payload}"));
            Assert.Fail($"Expected TiVo to receive a message containing '{expectedCommand}', but it was not found. Recorded messages: {recordedMessages}");
        }

        TestContext.WriteLine($"Successfully verified TiVo received message containing: {expectedCommand}");
    }
}
