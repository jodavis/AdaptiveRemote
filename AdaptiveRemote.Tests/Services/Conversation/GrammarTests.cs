using System.Reflection;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using AdaptiveRemote.Services.Configuration;
using AdaptiveRemote.TestUtilities;
using AdaptiveRemote.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Conversation;

[TestClass]
public class GrammarTests
{
    private readonly CancellationTokenSource _cts = new();
    private readonly TestAudioConfigurationService audioConfiguration = new();

    private static readonly List<(string, string)> TestPhrases = new()
    {
        ("Advance", "command=Skip,repeat=1"),
        ("Back", "command=Back,repeat=1"),
        ("Channel down", "command=ChannelDown,repeat=1"),
        ("Channel up", "command=ChannelUp,repeat=1"),
        ("Down", "command=Down,repeat=1"),
        ("Exit", "command=Exit"),
        ("Go back", "command=Back,repeat=1"),
        ("Go down", "command=Down,repeat=1"),
        ("Go left", "command=Left,repeat=1"),
        ("Go right", "command=Right,repeat=1"),
        ("Go to Guide", "command=Guide"),
        ("Go to Netflix", "command=Netflix"),
        ("Go to TiVo", "command=TiVo"),
        ("Go up", "command=Up,repeat=1"),
        ("Go up twice", "command=Up,repeat=2"),
        ("Guide", "command=Guide"),
        ("Left", "command=Left,repeat=1"),
        ("Louder", "command=VolumeUp,repeat=1"),
        ("Netflix", "command=Netflix"),
        ("OK", "command=Select"),
        ("Page down", "command=ChannelDown,repeat=1"),
        ("Page up", "command=ChannelUp,repeat=1"),
        ("Pause", "command=Pause"),
        ("Play", "command=Play"),
        ("Quieter", "command=VolumeDown,repeat=1"),
        ("Quit", "command=Exit"),
        ("Record", "command=Record"),
        ("Replay", "command=Replay,repeat=1"),
        ("Right", "command=Right,repeat=1"),
        ("Select", "command=Select"),
        ("Skip back", "command=Replay,repeat=1"),
        ("Skip forward", "command=Skip,repeat=1"),
        ("Skip", "command=Skip,repeat=1"),
        ("Softer", "command=VolumeDown,repeat=1"),
        ("TiVo", "command=TiVo"),
        ("Up", "command=Up,repeat=1"),
        ("Volume down", "command=VolumeDown,repeat=1"),
        ("Volume up", "command=VolumeUp,repeat=1"),
        ("Go up once", "command=Up,repeat=1"),
        ("Go down one", "command=Down,repeat=1"),
        ("Left one time", "command=Left,repeat=1"),
        ("Go right twice", "command=Right,repeat=2"),
        ("Volume down two", "command=VolumeDown,repeat=2"),
        ("Channel up two times", "command=ChannelUp,repeat=2"),
        ("Channel down three", "command=ChannelDown,repeat=3"),
        ("Replay three times", "command=Replay,repeat=3"),
        ("Skip back four", "command=Replay,repeat=4"),
        ("Skip four times", "command=Skip,repeat=4"),
        ("Page up six", "command=ChannelUp,repeat=6"),
        ("Page down six times", "command=ChannelDown,repeat=6"),
        ("Volume up seven", "command=VolumeUp,repeat=7"),
        ("Up seven times", "command=Up,repeat=7"),
        ("Down eight", "command=Down,repeat=8"),
        ("Left eight times", "command=Left,repeat=8"),
        ("Right nine times", "command=Right,repeat=9"),
        ("Go back nine", "command=Back,repeat=9"),
        ("Advance", "command=Skip,repeat=1"),
        ("Back", "command=Back,repeat=1"),
        ("Channel down", "command=ChannelDown,repeat=1"),
        ("Channel up", "command=ChannelUp,repeat=1"),
        ("Down", "command=Down,repeat=1"),
        ("Exit", "command=Exit"),
        ("Go back", "command=Back,repeat=1"),
        ("Go down", "command=Down,repeat=1"),
        ("Go left", "command=Left,repeat=1"),
        ("Go right", "command=Right,repeat=1"),
        ("Go to Guide", "command=Guide"),
        ("Go to Netflix", "command=Netflix"),
        ("Go to TiVo", "command=TiVo"),
        ("Go up", "command=Up,repeat=1"),
        ("Go up twice", "command=Up,repeat=2"),
        ("Guide", "command=Guide"),
        ("Left", "command=Left,repeat=1"),
        ("Louder", "command=VolumeUp,repeat=1"),
        ("Netflix", "command=Netflix"),
        ("OK", "command=Select"),
        ("Page down", "command=ChannelDown,repeat=1"),
        ("Page up", "command=ChannelUp,repeat=1"),
        ("Pause", "command=Pause"),
        ("Play", "command=Play"),
        ("Quieter", "command=VolumeDown,repeat=1"),
        ("Quit", "command=Exit"),
        ("Record", "command=Record"),
        ("Replay", "command=Replay,repeat=1"),
        ("Right", "command=Right,repeat=1"),
        ("Select", "command=Select"),
        ("Skip back", "command=Replay,repeat=1"),
        ("Skip forward", "command=Skip,repeat=1"),
        ("Skip", "command=Skip,repeat=1"),
        ("Softer", "command=VolumeDown,repeat=1"),
        ("TiVo", "command=TiVo"),
        ("Up", "command=Up,repeat=1"),
        ("Volume down", "command=VolumeDown,repeat=1"),
        ("Volume up", "command=VolumeUp,repeat=1"),
        ("Go up once", "command=Up,repeat=1"),
        ("Go down one", "command=Down,repeat=1"),
        ("Left one time", "command=Left,repeat=1"),
        ("Go right twice", "command=Right,repeat=2"),
        ("Volume down two", "command=VolumeDown,repeat=2"),
        ("Channel up two times", "command=ChannelUp,repeat=2"),
        ("Channel down three", "command=ChannelDown,repeat=3"),
        ("Replay three times", "command=Replay,repeat=3"),
        ("Skip back four", "command=Replay,repeat=4"),
        ("Skip four times", "command=Skip,repeat=4"),
        ("Page up six", "command=ChannelUp,repeat=6"),
        ("Page down six times", "command=ChannelDown,repeat=6"),
        ("Volume up seven", "command=VolumeUp,repeat=7"),
        ("Up seven times", "command=Up,repeat=7"),
        ("Down eight", "command=Down,repeat=8"),
        ("Left eight times", "command=Left,repeat=8"),
        ("Right nine times", "command=Right,repeat=9"),
        ("Go back nine", "command=Back,repeat=9"),
    };

    [TestCleanup]
    public void CancelTasks()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    [TestMethod]
    [Timeout(5000)]
    [DynamicData(nameof(GetTestSamples), DynamicDataSourceType.Method,
        DynamicDataDisplayName = nameof(GetTestSampleDisplayName))]
    public async Task StaticGrammar_TestCommand(
        string waveFileName,
        string expectedText,
        string expectedSemantics)
    {
        // Arrange
        ISpeechRecognition speechRecognition = CreateSut();

        audioConfiguration.SetAudioInputToWaveStream(waveFileName);

        Task timeoutTask = Task.Delay(2000, _cts.Token);

        // Act
        Task<IRecognitionResult> resultTask = GetFirstResult(speechRecognition, _cts.Token);
        await Task.WhenAny(resultTask, timeoutTask);

        // Assert
        TaskAssert.IsComplete(resultTask, nameof(resultTask) + " timed out");
        IRecognitionResult result = resultTask.Result;

        Assert.AreEqual(expectedText, result.Text, nameof(result) + "." + nameof(result.Text));

        foreach (string expectedSemantic in expectedSemantics.Split(","))
        {
            string[] parts = expectedSemantic.Split("=");
            Assert.AreEqual(2, parts.Length, "Invalid semantic format '{0}'", expectedSemantic);

            string expectedKey = parts[0];
            string expectedValue = parts[1];

            Assert.IsTrue(result.TryGetSemanticValue(expectedKey, out string? resultValue), "Did not find semantic key '{0}'", expectedKey);
            Assert.AreEqual(expectedValue, resultValue, "Wrong value for semantic key '{0}'", expectedKey);
        }
    }

    private static async Task<IRecognitionResult> GetFirstResult(ISpeechRecognition speechRecognition, CancellationToken cancellationToken)
    {
        await foreach (IRecognitionResult result in speechRecognition.ListenForCommandsAsync(cancellationToken))
        {
            return result;
        }

        Assert.Fail("No results were returned from speech recognition");
        return default!; // We won't get here.
    }

    private ISpeechRecognition CreateSut()
        => CreateTestHost().Services.GetRequiredService<ISpeechRecognition>();

    private IHost CreateTestHost() =>
        Host.CreateDefaultBuilder()
            .AddConversationSystem()
            .ConfigureServices(s => s.AddSingleton<IAudioConfigurationService>(audioConfiguration))
            .Build();

    public static IEnumerable<object[]> GetTestSamples()
    {
        foreach ((string text, string semantics) testPhrase in TestPhrases)
        {
            string searchString = $"_{testPhrase.text.ToPascalCase()}_";
            bool foundOne = false;
            foreach (string resourceName in typeof(GrammarTests).Assembly.GetManifestResourceNames())
            {
                if (resourceName.Contains(searchString))
                {
                    foundOne = true;
                    yield return new object[]
                    {
                        resourceName,
                        testPhrase.text,
                        testPhrase.semantics
                    };
                }
            }

            if (!foundOne)
            {
                yield return new object[]
                {
                    $"{typeof(GrammarTests).Namespace}.SpeechSamples.*{searchString}*.wav",
                    testPhrase.text,
                    testPhrase.semantics
                };
            }
        }
    }

    public static string GetTestSampleDisplayName(MethodInfo testMethod, object[] inputs)
    {
        string[] parts = ((string)inputs[0]).Split(".");
        string name = parts[parts.Length - 2];
        return $"{testMethod.Name}({name})";
    }

    private class TestAudioConfigurationService : IAudioConfigurationService
    {
        private SpeechRecognitionEngine? _recognition;

        void IAudioConfigurationService.Configure(SpeechRecognitionEngine recognition) => _recognition = recognition
            ?? throw new ArgumentNullException(nameof(recognition));
        void IAudioConfigurationService.Configure(SpeechSynthesizer synthesizer) => throw new NotImplementedException();

        internal void SetAudioInputToWaveStream(string waveFileName)
        {
            Assert.IsNotNull(_recognition, "{0}.{1}({2}) was not called",
                nameof(IAudioConfigurationService),
                nameof(IAudioConfigurationService.Configure),
                nameof(SpeechRecognitionEngine));

            Stream? waveFile = typeof(GrammarTests).Assembly.GetManifestResourceStream(waveFileName);
            Assert.IsNotNull(waveFile, "Could not load resource {0}", waveFileName);

            _recognition.SetInputToWaveStream(waveFile);
        }
    }
}
