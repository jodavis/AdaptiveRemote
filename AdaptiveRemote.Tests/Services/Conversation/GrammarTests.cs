using System.Speech.Recognition;
using System.Speech.Synthesis;
using AdaptiveRemote.Services.Configuration;
using AdaptiveRemote.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.Services.Conversation;

[TestClass]
public class GrammarTests
{
    private readonly CancellationTokenSource _cts = new();
    private readonly TestAudioConfigurationService audioConfiguration = new();

    [TestCleanup]
    public void CancelTasks()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    [TestMethod]
    [Timeout(2000)]
    [DataRow("User1_Advance_032138", "Advance", "command=Skip,repeat=1", DisplayName = "User1_Advance_032138")]
    [DataRow("User1_Back_032109", "Back", "command=Back,repeat=1", DisplayName = "User1_Back_032109")]
    [DataRow("User1_ChannelDown_032155", "Channel down", "command=ChannelDown,repeat=1", DisplayName = "User1_ChannelDown_032155")]
    [DataRow("User1_ChannelUp_032151", "Channel up", "command=ChannelUp,repeat=1", DisplayName = "User1_ChannelUp_032151")]
    [DataRow("User1_Down_032015", "Down", "command=Down,repeat=1", DisplayName = "User1_Down_032015")]
    [DataRow("User1_Exit_032230", "Exit", "command=Exit", DisplayName = "User1_Exit_032230")]
    [DataRow("User1_GoBack_032112", "Go back", "command=Back,repeat=1", DisplayName = "User1_GoBack_032112")]
    [DataRow("User1_GoDown_032019", "Go down", "command=Down,repeat=1", DisplayName = "User1_GoDown_032019")]
    [DataRow("User1_GoLeft_032025", "Go left", "command=Left,repeat=1", DisplayName = "User1_GoLeft_032025")]
    [DataRow("User1_GoRight_032032", "Go right", "command=Right,repeat=1", DisplayName = "User1_GoRight_032032")]
    [DataRow("User1_GoToGuide_032057", "Go to Guide", "command=Guide", DisplayName = "User1_GoToGuide_032057")]
    [DataRow("User1_GoToNetflix_032105", "Go to Netflix", "command=Netflix", DisplayName = "User1_GoToNetflix_032105")]
    [DataRow("User1_GoToTiVo_032049", "Go to TiVo", "command=TiVo", DisplayName = "User1_GoToTiVo_032049")]
    [DataRow("User1_GoUp_032009", "Go up", "command=Up,repeat=1", DisplayName = "User1_GoUp_032009")]
    [DataRow("User1_GoUpTwice_104425", "Go up twice", "command=Up,repeat=2", DisplayName = "User1_GoUpTwice_104425")]
    [DataRow("User1_Guide_032053", "Guide", "command=Guide", DisplayName = "User1_Guide_032053")]
    [DataRow("User1_Left_032022", "Left", "command=Left,repeat=1", DisplayName = "User1_Left_032022")]
    [DataRow("User1_Louder_032205", "Louder", "command=VolumeUp,repeat=1", DisplayName = "User1_Louder_032205")]
    [DataRow("User1_Netflix_032101", "Netflix", "command=Netflix", DisplayName = "User1_Netflix_032101")]
    [DataRow("User1_OK_032041", "OK", "command=Select", DisplayName = "User1_OK_032041")]
    [DataRow("User1_PageDown_032202", "Page down", "command=ChannelDown,repeat=1", DisplayName = "User1_PageDown_032202")]
    [DataRow("User1_PageUp_032158", "Page up", "command=ChannelUp,repeat=1", DisplayName = "User1_PageUp_032158")]
    [DataRow("User1_Pause_032124", "Pause", "command=Pause", DisplayName = "User1_Pause_032124")]
    [DataRow("User1_Play_032116", "Play", "command=Play", DisplayName = "User1_Play_032116")]
    [DataRow("User1_Quieter_032212", "Quieter", "command=VolumeDown,repeat=1", DisplayName = "User1_Quieter_032212")]
    [DataRow("User1_Quit_032234", "Quit", "command=Exit", DisplayName = "User1_Quit_032234")]
    [DataRow("User1_Record_032127", "Record", "command=Record", DisplayName = "User1_Record_032127")]
    [DataRow("User1_Replay_032131", "Replay", "command=Replay,repeat=1", DisplayName = "User1_Replay_032131")]
    [DataRow("User1_Right_032029", "Right", "command=Right,repeat=1", DisplayName = "User1_Right_032029")]
    [DataRow("User1_Select_032037", "Select", "command=Select", DisplayName = "User1_Select_032037")]
    [DataRow("User1_SkipBack_032134", "Skip back", "command=Replay,repeat=1", DisplayName = "User1_SkipBack_032134")]
    [DataRow("User1_SkipForward_032141", "Skip forward", "command=Skip,repeat=1", DisplayName = "User1_SkipForward_032141")]
    [DataRow("User1_Skip_034011", "Skip", "command=Skip,repeat=1", DisplayName = "User1_Skip_032147")]
    [DataRow("User1_Softer_032209", "Softer", "command=VolumeDown,repeat=1", DisplayName = "User1_Softer_032209")]
    [DataRow("User1_TiVo_032045", "TiVo", "command=TiVo", DisplayName = "User1_TiVo_032045")]
    [DataRow("User1_Up_094457", "Up", "command=Up,repeat=1", DisplayName = "User1_Up_094457")]
    [DataRow("User1_VolumeDown_032221", "Volume down", "command=VolumeDown,repeat=1", DisplayName = "User1_VolumeDown_032221")]
    [DataRow("User1_VolumeUp_032217", "Volume up", "command=VolumeUp,repeat=1", DisplayName = "User1_VolumeUp_032217")]
    [DataRow("User1_GoUpOnce_103013", "Go up once", "command=Up,repeat=1", DisplayName = "User1_GoUpOnce_103013")]
    [DataRow("User1_GoDownOne_103016", "Go down one", "command=Down,repeat=1", DisplayName = "User1_GoDownOne_103016")]
    [DataRow("User1_LeftOneTime_103019", "Left one time", "command=Left,repeat=1", DisplayName = "User1_LeftOneTime_103019")]
    [DataRow("User1_GoRightTwice_103023", "Go right twice", "command=Right,repeat=2", DisplayName = "User1_GoRightTwice_103023")]
    [DataRow("User1_VolumeDownTwo_112259", "Volume down two", "command=VolumeDown,repeat=2", DisplayName = "User1_VolumeDownTwo_112259")]
    [DataRow("User1_ChannelUpTwoTimes_103041", "Channel up two times", "command=ChannelUp,repeat=2", DisplayName = "User1_ChannelUpTwoTimes_103041")]
    [DataRow("User1_ChannelDownThree_103045", "Channel down three", "command=ChannelDown,repeat=3", DisplayName = "User1_ChannelDownThree_103045")]
    [DataRow("User1_ReplayThreeTimes_103050", "Replay three times", "command=Replay,repeat=3", DisplayName = "User1_ReplayThreeTimes_103050")]
    [DataRow("User1_SkipBackFour_112253", "Skip back four", "command=Replay,repeat=4", DisplayName = "User1_SkipBackFour_112253")]
    [DataRow("User1_SkipFourTimes_103100", "Skip four times", "command=Skip,repeat=4", DisplayName = "User1_SkipFourTimes_103100")]
    [DataRow("User1_PageUpSix_103112", "Page up six", "command=ChannelUp,repeat=6", DisplayName = "User1_PageUpSix_103112")]
    [DataRow("User1_PageDownSixTimes_103116", "Page down six times", "command=ChannelDown,repeat=6", DisplayName = "User1_PageDownSixTimes_103116")]
    [DataRow("User1_VolumeUpSeven_103120", "Volume up seven", "command=VolumeUp,repeat=7", DisplayName = "User1_VolumeUpSeven_103120")]
    [DataRow("User1_UpSevenTimes_103127", "Up seven times", "command=Up,repeat=7", DisplayName = "User1_UpSevenTimes_103127")]
    [DataRow("User1_DownEight_112249", "Down eight", "command=Down,repeat=8", DisplayName = "User1_DownEight_112249")]
    [DataRow("User1_LeftEightTimes_103136", "Left eight times", "command=Left,repeat=8", DisplayName = "User1_LeftEightTimes_103136")]
    [DataRow("User1_RightNineTimes_103141", "Right nine times", "command=Right,repeat=9", DisplayName = "User1_RightNineTimes_103141")]
    [DataRow("User1_GoBackNine_103145", "Go back nine", "command=Back,repeat=9", DisplayName = "User1_GoBackNine_103145")]
    public async Task StaticGrammar_TestCommands(
        string waveFileName,
        string expectedText,
        string expectedSemantics)
    {
        // Arrange
        ISpeechRecognition speechRecognition = CreateSut();

        audioConfiguration.SetAudioInputToWaveStream(waveFileName);

        Task timeoutTask = Task.Delay(1000, _cts.Token);

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

            string resourceName = $"{typeof(GrammarTests).Namespace}.SpeechSamples.{waveFileName}.wav";
            Stream? waveFile = typeof(GrammarTests).Assembly.GetManifestResourceStream(resourceName);
            Assert.IsNotNull(waveFile, "Could not load resource {0}", resourceName);

            _recognition.SetInputToWaveStream(waveFile);
        }
    }
}
