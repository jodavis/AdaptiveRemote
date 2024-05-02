using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text;
using AdaptiveRemote.Services.Configuration;
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

    //[TestMethod]
    public async Task StaticGrammar_Record()
    {
        // Arrange
        const string UserName = "User1";
        const string OutputPath = @"C:\OutputDir";

        IAudioConfigurationService audioConfiguration = new RecordingAudioConfigurationService(UserName, OutputPath);

        IHost host =
        Host.CreateDefaultBuilder()
            .AddConversationServices()
            .ConfigureServices(s => s.AddSingleton(audioConfiguration))
            .Build();

        ISpeechRecognition recognition = host.Services.GetRequiredService<ISpeechRecognition>();

        // Act
        await foreach (IRecognitionResult result in recognition.ListenForCommandsAsync(_cts.Token))
        {
        }
    }

    [TestMethod]
    [Timeout(5000)]
    [DataRow("User1_Advance_032138", "Advance", "Command:Skip", DisplayName = "User1_Advance_032138")]
    [DataRow("User1_Back_032109", "Back", "Command:Back", DisplayName = "User1_Back_032109")]
    [DataRow("User1_ChannelDown_032155", "Channel down", "Command:ChannelDown", DisplayName = "User1_ChannelDown_032155")]
    [DataRow("User1_ChannelUp_032151", "Channel up", "Command:ChannelUp", DisplayName = "User1_ChannelUp_032151")]
    [DataRow("User1_Down_032015", "Down", "Command:Down", DisplayName = "User1_Down_032015")]
    [DataRow("User1_Exit_032230", "Exit", "Command:Exit", DisplayName = "User1_Exit_032230")]
    [DataRow("User1_GoBack_032112", "Go back", "Command:Back", DisplayName = "User1_GoBack_032112")]
    [DataRow("User1_GoDown_032019", "Go down", "Command:Down", DisplayName = "User1_GoDown_032019")]
    [DataRow("User1_GoLeft_032025", "Go left", "Command:Left", DisplayName = "User1_GoLeft_032025")]
    [DataRow("User1_GoRight_032032", "Go right", "Command:Right", DisplayName = "User1_GoRight_032032")]
    [DataRow("User1_GoToGuide_032057", "Go to Guide", "Command:Guide", DisplayName = "User1_GoToGuide_032057")]
    [DataRow("User1_GoToNetflix_032105", "Go to Netflix", "Command:Netflix", DisplayName = "User1_GoToNetflix_032105")]
    [DataRow("User1_GoToTiVo_032049", "Go to TiVo", "Command:TiVo", DisplayName = "User1_GoToTiVo_032049")]
    [DataRow("User1_GoUp_032009", "Go up", "Command:Up", DisplayName = "User1_GoUp_032009")]
    [DataRow("User1_Guide_032053", "Guide", "Command:Guide", DisplayName = "User1_Guide_032053")]
    [DataRow("User1_Left_032022", "Left", "Command:Left", DisplayName = "User1_Left_032022")]
    [DataRow("User1_Louder_032205", "Louder", "Command:VolumeUp", DisplayName = "User1_Louder_032205")]
    [DataRow("User1_Netflix_032101", "Netflix", "Command:Netflix", DisplayName = "User1_Netflix_032101")]
    [DataRow("User1_OK_032041", "OK", "Command:Select", DisplayName = "User1_OK_032041")]
    [DataRow("User1_PageDown_032202", "Page down", "Command:ChannelDown", DisplayName = "User1_PageDown_032202")]
    [DataRow("User1_PageUp_032158", "Page up", "Command:ChannelUp", DisplayName = "User1_PageUp_032158")]
    [DataRow("User1_Pause_032124", "Pause", "Command:Pause", DisplayName = "User1_Pause_032124")]
    [DataRow("User1_Play_032116", "Play", "Command:Play", DisplayName = "User1_Play_032116")]
    [DataRow("User1_Quieter_032212", "Quieter", "Command:VolumeDown", DisplayName = "User1_Quieter_032212")]
    [DataRow("User1_Quit_032234", "Quit", "Command:Exit", DisplayName = "User1_Quit_032234")]
    [DataRow("User1_Record_032127", "Record", "Command:Record", DisplayName = "User1_Record_032127")]
    [DataRow("User1_Replay_032131", "Replay", "Command:Replay", DisplayName = "User1_Replay_032131")]
    [DataRow("User1_Right_032029", "Right", "Command:Right", DisplayName = "User1_Right_032029")]
    [DataRow("User1_Select_032037", "Select", "Command:Select", DisplayName = "User1_Select_032037")]
    [DataRow("User1_SkipBack_032134", "Skip back", "Command:Replay", DisplayName = "User1_SkipBack_032134")]
    [DataRow("User1_SkipForward_032141", "Skip forward", "Command:Skip", DisplayName = "User1_SkipForward_032141")]
    [DataRow("User1_Skip_034011", "Skip", "Command:Skip", DisplayName = "User1_Skip_032147")]
    [DataRow("User1_Softer_032209", "Softer", "Command:VolumeDown", DisplayName = "User1_Softer_032209")]
    [DataRow("User1_StopListening_032238", "Stop listening", "StopListening", DisplayName = "User1_StopListening_032238")]
    [DataRow("User1_TiVo_032045", "TiVo", "Command:TiVo", DisplayName = "User1_TiVo_032045")]
    [DataRow("User1_Up_032006", "Up", "Command:Up", DisplayName = "User1_Up_032006")]
    [DataRow("User1_VolumeDown_032221", "Volume down", "Command:VolumeDown", DisplayName = "User1_VolumeDown_032221")]
    [DataRow("User1_VolumeUp_032217", "Volume up", "Command:VolumeUp", DisplayName = "User1_VolumeUp_032217")]
    public async Task StaticGrammar_Test(
        string waveFileName,
        string expectedText,
        string expectedSemantics)
    {
        // Arrange
        ISpeechRecognition speechRecognition = CreateSut();

        audioConfiguration.SetAudioInputToWaveStream(waveFileName);

        // Act
        await foreach (IRecognitionResult result in speechRecognition.ListenForCommandsAsync(_cts.Token))
        {
            // Assert
            Assert.AreEqual(expectedText, result.Text, nameof(result) + "." + nameof(result.Text));
            Assert.AreEqual(expectedSemantics, result.SemanticMeaning, nameof(result) + "." + nameof(result.SemanticMeaning));

            break;
        }
    }

    private ISpeechRecognition CreateSut()
        => CreateTestHost().Services.GetRequiredService<ISpeechRecognition>();

    private IHost CreateTestHost() =>
        Host.CreateDefaultBuilder()
            .AddConversationServices()
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

    private class RecordingAudioConfigurationService : IAudioConfigurationService
    {
        private readonly string _userName;
        private readonly string _outputPath;

        public RecordingAudioConfigurationService(string userName, string outputPath)
        {
            _userName = userName;
            _outputPath = outputPath;
        }

        public void Configure(SpeechRecognitionEngine recognition)
        {
            recognition.SpeechRecognized += RecordSpeech;
            recognition.SetInputToDefaultAudioDevice();
        }

        private void RecordSpeech(object? sender, SpeechRecognizedEventArgs e)
        {
            string text = PascalCase(e.Result.Text);
            string path = $"{_outputPath}\\{_userName}_{text}_{DateTime.Now:hhmmss}.wav";
            using (Stream waveStream = File.OpenWrite(path))
            {
                e.Result.Audio.WriteToWaveStream(waveStream);
                waveStream.Flush();
            }
        }

        private static string PascalCase(string text)
        {
            StringBuilder builder = new();

            bool nextUpper = true;
            foreach (char c in text)
            {
                if (Char.IsWhiteSpace(c))
                {
                    nextUpper = true;
                }
                else if (nextUpper)
                {
                    nextUpper = false;
                    builder.Append(Char.ToUpperInvariant(c));
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        public void Configure(SpeechSynthesizer synthesizer) => throw new NotImplementedException();
    }
}
