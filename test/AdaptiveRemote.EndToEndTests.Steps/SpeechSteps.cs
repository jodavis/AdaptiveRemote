using AdaptiveRemote.EndtoEndTests;
using AdaptiveRemote.EndtoEndTests.Host;
using AdaptiveRemote.EndtoEndTests.Logging;
using AdaptiveRemote.Services.Conversation;
using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class SpeechSteps : StepsBase
{
    private ITestSpeechRecognitionEngine? _testSpeechEngine;

    /// <summary>
    /// Special setup for speech recognition tests - registers a custom host factory that injects TestSpeechRecognitionEngine.
    /// This runs BEFORE the standard HostSteps BeforeScenario.
    /// </summary>
    [BeforeScenario("@speech", Order = 100)]
    public void OnBeforeScenario_SetUpSpeechTestHost(AdaptiveRemoteHostSettings hostSettings)
    {
        TestContext.WriteLine("[SpeechSteps] Setting up custom host factory with TestSpeechRecognitionEngine injection");

        // Check if host factory is already set up (from HostSteps)
        // If so, skip this - we'll just use the normal host
        // This scenario setup runs at Order 100, before HostSteps Order 200

        // Use the simulated TiVo device
        string tivoArgs = "--tivo:Fake=True";
        if (Environment.TiVo != null)
        {
            tivoArgs = $"--tivo:IP=127.0.0.1:{Environment.TiVo.Port}";
        }

        // Use the simulated Broadlink device
        string broadlinkArgs = string.Empty;
        if (Environment.Broadlink != null)
        {
            broadlinkArgs = $"--broadlink:DiscoveryAddress=127.0.0.1 --broadlink:DiscoveryPort={Environment.Broadlink.Port}";
        }

        string logFilePath = Path.Combine(TestContext.TestResultsDirectory!, TestContext.TestName + ".log");
        hostSettings = hostSettings.AddCommandLineArgs($"{tivoArgs} {broadlinkArgs} --log:FilePath=\"{logFilePath}\"");

        TestContext.WriteLine("[SpeechSteps] Creating host factory with service injection callback");

        // Provide a custom host factory that injects TestSpeechRecognitionEngine
        ProvideContainerObjectFactory(() =>
        {
            TestContext.WriteLine("[SpeechSteps] Host factory invoked, creating builder");
            return AdaptiveRemoteHost.CreateBuilder(hostSettings)
                .ConfigureLogging(builder =>
                {
                    builder.AddDebug();
                    builder.AddTestContext(TestContext);
                })
                .ConfigureTestServices(async (testEndpoint, ct) =>
                {
                    // Inject TestSpeechRecognitionEngine before the host builds
                    TestContext.WriteLine("[SpeechSteps] Injecting TestSpeechRecognitionEngine");
                    await testEndpoint.InjectTestServiceAsync<ISpeechRecognitionEngine, TestSpeechRecognitionEngine>(ct);
                    TestContext.WriteLine("[SpeechSteps] TestSpeechRecognitionEngine injected");
                })
                .Start();
        });

        TestContext.WriteLine("[SpeechSteps] Custom host factory registered");
    }

    [Given(@"the application is running with test speech recognition")]
    public async Task GivenTheApplicationIsRunningWithTestSpeechRecognitionAsync()
    {
        // Start the application (this will use our custom factory with service injection)
        if (!IsHostRunning)
        {
            _ = Host; // Trigger lazy initialization with our custom factory
        }

        // Wait for the application to be ready
        Host.Application.WaitForPhase(
            LifecyclePhase.Ready,
            timeout: TimeSpan.FromSeconds(60));

        // Get the test speech engine from the host
        _testSpeechEngine = await Host.Application.GetTestSpeechEngineAsync(CancellationToken.None);

        Assert.IsNotNull(_testSpeechEngine, "Test speech recognition engine was not injected into the host");
    }

    [When(@"I say ""(.*)""")]
    public async Task WhenISayAsync(string phrase)
    {
        Assert.IsNotNull(_testSpeechEngine, "Test speech engine is not available");
        await _testSpeechEngine.SpeakAsync(phrase);

        // Give the application time to process the speech input
        await Task.Delay(500);
    }

    [Then(@"the application should enter listening mode")]
    public async Task ThenTheApplicationShouldEnterListeningModeAsync()
    {
        // Check that the application is in listening mode
        bool isListening = await Host.Application.GetIsListeningAsync(CancellationToken.None);
        Assert.IsTrue(isListening, "Application should be in listening mode after wake word");
    }

    [Then(@"the application should exit listening mode")]
    public async Task ThenTheApplicationShouldExitListeningModeAsync()
    {
        // Check that the application has exited listening mode
        bool isListening = await Host.Application.GetIsListeningAsync(CancellationToken.None);
        Assert.IsFalse(isListening, "Application should have exited listening mode after stop phrase");
    }
}
