using System.Runtime.CompilerServices;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Conversation;

internal class ConversationController : IScopedLifecycle, IDisposable
{
    private readonly ConversationSettings _speechSettings;
    private readonly ISpeechRecognition _speechRecognition;
    private readonly ISpeechSynthesis _speechSynthesis;
    private readonly ICommandExecutionService _executionService;
    private readonly IRemoteDefinitionService _definitionService;
    private readonly ILogger<ConversationController> _logger;
    private readonly ConversationView _viewModel;

    private readonly CancellationTokenSource _stop = new();

    public ConversationController(
        IOptionsSnapshot<ConversationSettings> options,
        ISpeechRecognition speechRecognition,
        ISpeechSynthesis speechSynthesis,
        IRemoteDefinitionService definitionService,
        ICommandExecutionService executionService,
        ILogger<ConversationController> logger,
        ConversationView viewModel)
    {
        _speechSettings = options.Value;
        _speechRecognition = speechRecognition;
        _speechSynthesis = speechSynthesis;
        _executionService = executionService;
        _definitionService = definitionService;
        _logger = logger;
        _viewModel = viewModel;

        _viewModel.IsListening = false;
        _viewModel.StatusMessage = Phrases.Conversation_WaitingForActivation;
    }

    string IScopedLifecycle.Name => "Conversation system";

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, Command>? commands = GetCommands();

        if (commands is not null)
        {
            _ = ListenWithRetriesAsync(commands, _stop.Token);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _logger.LogInformation(Message.ConversationController_Stopping);
        _stop.Cancel();
    }

    private IReadOnlyDictionary<string, Command>? GetCommands()
    {
        Dictionary<string, Command> commands = new(StringComparer.Ordinal);
        try
        {
            foreach (Command command in _definitionService.GetCommands())
            {
                commands[command.Name] = command;
            }

            return commands;
        }
        catch (Exception ex)
        {
            _logger.LogError(Message.ConversationController_ErrorDuringStartup, ex);
            _viewModel.StatusMessage = Phrases.Conversation_SystemFailed;

            return null;
        }
    }

    private async Task ListenWithRetriesAsync(IReadOnlyDictionary<string, Command> commands, CancellationToken cancellationToken)
    {
        int errorCount = 0;
        while (true)
        {
            try
            {
                await ListenAsync(commands, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation(Message.ConversationController_Stopped);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(Message.ConversationController_Error, ex);

                errorCount++;
                if (errorCount >= _speechSettings.ErrorRetryLimit)
                {
                    _logger.LogWarning(Message.ConversationController_RetryLimitReached, errorCount);
                    _viewModel.StatusMessage = Phrases.Conversation_SystemFailed;
                    break;
                }
                else
                {
                    _logger.LogWarning(Message.ConversationController_Retrying, errorCount);
                }
            }
        }
    }

    private async Task ListenAsync(IReadOnlyDictionary<string, Command> commands, CancellationToken cancellationToken)
    {
        while (true)
        {
            await ListenForAttentionAsync(cancellationToken);

            await foreach (IRecognitionResult result in ListenForCommandsAsync(cancellationToken))
            {
                if (result.TryGetSemanticValue("command", out string? commandName))
                {
                    _logger.LogInformation(Message.ConversationController_Recognized, result.Text, commandName);

                    if (commands.TryGetValue(commandName, out Command? command))
                    {
                        int repeat = ParseRepeat(result);

                        await ExecuteCommandAsync(command, repeat, cancellationToken);
                    }
                    else
                    {
                        _logger.LogError(Message.ConversationController_UnknownCommand, result.Text);
                    }
                }
            }
        }

        static int ParseRepeat(IRecognitionResult result)
        {
            if (result.TryGetSemanticValue("repeat", out string? repeatString) &&
                int.TryParse(repeatString, out int repeat))
            {
                return repeat;
            }

            return 1;
        }
    }

    private async Task ListenForAttentionAsync(CancellationToken cancellationToken)
    {
        try
        {
            _viewModel.StatusMessage = Phrases.Conversation_ListeningForAttention;
            _logger.LogInformation(Message.ConversationController_ListenForAttention);

            await _speechRecognition.ListenForAttentionAsync(cancellationToken);
        }
        finally
        {
            _viewModel.StatusMessage = string.Empty;
        }
    }

    private async IAsyncEnumerable<IRecognitionResult> ListenForCommandsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            _viewModel.StatusMessage = Phrases.Conversation_ImListening;
            await SayAsync(Phrases.Conversation_ImListening, cancellationToken);

            _viewModel.IsListening = true;
            _logger.LogInformation(Message.ConversationController_ListenForCommands);

            await foreach (IRecognitionResult result in _speechRecognition.ListenForCommandsAsync(cancellationToken))
            {
                yield return result;
            }
        }
        finally
        {
            _viewModel.IsListening = false;
            _viewModel.StatusMessage = string.Empty;
        }

        await SayAsync(Phrases.Conversation_StoppedListening, cancellationToken);
    }

    private async Task ExecuteCommandAsync(Command command, int repeat, CancellationToken cancellationToken)
    {
        Task sayTask = SayAsync(Phrases.Conversation_Sent(command.Name, repeat), cancellationToken);

        string previousMessage = _viewModel.StatusMessage;
        _viewModel.StatusMessage = Phrases.Conversation_ImSending;

        try
        {
            for (int i = 0; i < repeat; i++)
            {
                _logger.LogInformation(Message.ConversationController_Executing, command.Name);

                await _executionService.ExecuteAsync(command, cancellationToken);

                _logger.LogInformation(Message.ConversationController_Executed, command.Name);
            }

            await sayTask;
        }
        finally
        {
            _viewModel.StatusMessage = previousMessage;
        }
    }

    private async Task SayAsync(string phrase, CancellationToken cancellationToken)
    {
        bool wasListening = _viewModel.IsListening;
        try
        {
            _viewModel.IsListening = false;
            _viewModel.SpeakingMessage = phrase;
            await _speechSynthesis.SayAsync(phrase, cancellationToken);
        }
        finally
        {
            _viewModel.IsListening = wasListening;
            _viewModel.SpeakingMessage = null;
        }
    }
}
