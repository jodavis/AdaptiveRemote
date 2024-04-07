using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Speech;

internal class SpeechController : ISpeechController, IDisposable
{
    private readonly SpeechSettings _speechSettings;
    private readonly ISpeechRecognition _speechRecognition;
    private readonly ISpeechSynthesis _speechSynthesis;
    private readonly ICommandExecutionService _executionService;
    private readonly IRemoteDefinitionService _definitionService;
    private readonly ILogger<SpeechController> _logger;
    private readonly Listening _viewModel;

    private readonly CancellationTokenSource _stop = new();

    public SpeechController(
        IOptionsSnapshot<SpeechSettings> options,
        ISpeechRecognition speechRecognition,
        ISpeechSynthesis speechSynthesis,
        IRemoteDefinitionService definitionService,
        ICommandExecutionService executionService,
        ILogger<SpeechController> logger,
        Listening viewModel)
    {
        _speechSettings = options.Value;
        _speechRecognition = speechRecognition;
        _speechSynthesis = speechSynthesis;
        _executionService = executionService;
        _definitionService = definitionService;
        _logger = logger;
        _viewModel = viewModel;

        _viewModel.IsListening = false;
        _viewModel.StatusMessage = Phrases.Speech_WaitingForActivation;
    }

    // TODO: This should not be async void!!!
    public async void Start()
    {
        Dictionary<string, Command> commands = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (Command command in _definitionService.GetCommands())
            {
                commands[command.Name] = command;
                foreach (string alternate in command.Alternates)
                {
                    commands[alternate] = command;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, LoggingMessages.SpeechController_ErrorDuringStartup, ex);
            _viewModel.StatusMessage = Phrases.Speech_ListeningSystemFailed;
            return;
        }

        int errorCount = 0;
        while (true)
        {
            try
            {
                _viewModel.StatusMessage = Phrases.Speech_ListeningForAttention;
                _logger.LogInformation(LoggingMessages.SpeechController_ListenForAttention);
                await _speechRecognition.ListenForAttention(_stop.Token);
                _viewModel.IsListening = true;
                _viewModel.StatusMessage = Phrases.Speech_ImListening;
                _speechSynthesis.Say(Phrases.Speech_ImListening);
                _logger.LogInformation(LoggingMessages.SpeechController_ListenForCommands);
                await foreach (IRecognitionResult result in _speechRecognition.ListenForCommands(CancellationToken.None))
                {
                    _logger.LogInformation(LoggingMessages.SpeechController_Recognized, result.Text);
                    if (commands.TryGetValue(result.Text, out Command? command))
                    {
                        _speechSynthesis.Say(Phrases.Speech_Sending(command.Name));
                        _logger.LogInformation(LoggingMessages.SpeechController_Executing, command.Name);
                        await _executionService.ExecuteAsync(command);
                        _logger.LogInformation(LoggingMessages.SpeechController_Executed, command.Name);
                    }
                }
                _speechSynthesis.Say(Phrases.Speech_StoppedListening);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation(LoggingMessages.SpeechController_Stopped);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, LoggingMessages.SpeechController_Error, ex);

                errorCount++;
                if (errorCount >= _speechSettings.ErrorRetryLimit)
                {
                    _logger.LogWarning(LoggingMessages.SpeechController_RetryLimitReached, errorCount);
                    _viewModel.StatusMessage = Phrases.Speech_ListeningSystemFailed;
                    break;
                }
                else
                {
                    _logger.LogInformation(LoggingMessages.SpeechController_Retrying, errorCount);
                }
            }
            finally
            {
                _viewModel.IsListening = false;
            }
        }
    }

    public void Dispose()
    {
        _logger.LogInformation(LoggingMessages.SpeechController_Stopping);
        _stop.Cancel();
        //_logger.LogInformation("Stopped");
        //_viewModel.StatusMessage = String.Empty;
        //_viewModel.IsListening = false;
    }
}
