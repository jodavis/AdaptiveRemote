using AdaptiveRemote.EndtoEndTests;
using AdaptiveRemote.EndtoEndTests.Host;
using AdaptiveRemote.Services.Conversation;
using AdaptiveRemote.Services.Lifecycle;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reqnroll;

namespace AdaptiveRemote.EndToEndTests.Steps;

[Binding]
public class SpeechSteps : StepsBase
{
    private ITestSpeechRecognitionEngine? _testSpeechEngine;
    private bool _hostStartedWithSpeechInjection = false;

    [Given(@"the application is running with test speech recognition")]
    public async Task GivenTheApplicationIsRunningWithTestSpeechRecognitionAsync()
    {
        // If host is already running, we need to stop it first
        if (IsHostRunning)
        {
            Host.Stop();
        }

        // Get the host builder settings from container
        AdaptiveRemoteHostSettings hostSettings = GetContainerObject<AdaptiveRemoteHostSettings>();

        // Create host builder
        AdaptiveRemoteHost.Builder builder = AdaptiveRemoteHost.CreateBuilder(hostSettings)
            .ConfigureLogging(loggingBuilder =>
            {
                loggingBuilder.AddDebug();
                loggingBuilder.AddTestContext(TestContext);
            });

        // Don't call Start() yet - we need to get the host to inject services first
        // Instead, we'll start the process and connect, inject the service, then signal to build
        AdaptiveRemoteHost host = builder.Start();

        // Now inject TestSpeechRecognitionEngine before the host builds
        await host.TestEndpoint.InjectTestServiceAsync<ISpeechRecognitionEngine, TestSpeechRecognitionEngine>(
            CancellationToken.None);

        // Register the host in the container so other steps can use it
        ProvideContainerObject(host);
        _hostStartedWithSpeechInjection = true;

        // Wait for the application to be ready
        host.Application.WaitForPhase(
            LifecyclePhase.Ready,
            timeout: TimeSpan.FromSeconds(60));

        // Get the test speech engine from the host
        _testSpeechEngine = await host.Application.GetTestSpeechEngineAsync(CancellationToken.None);

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

    private ObjectType GetContainerObject<ObjectType>()
        where ObjectType : notnull
    {
        // Use reflection to access the private _container field from StepsBase
        var containerField = typeof(StepsBase).GetField("_container",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(containerField, "Could not find _container field");

        var container = containerField.GetValue(this) as Reqnroll.BoDi.IObjectContainer;
        Assert.IsNotNull(container, "Container is null");

        return container.Resolve<ObjectType>();
    }
}
