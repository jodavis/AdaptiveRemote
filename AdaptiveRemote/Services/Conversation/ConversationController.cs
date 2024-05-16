using System.Runtime.CompilerServices;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Conversation;

internal class ConversationController : IConversationController, IDisposable
{
    private readonly ConversationSettings _speechSettings;
    private readonly ISpeechRecognition _speechRecognition;
    private readonly ISpeechSynthesis _speechSynthesis;
    private readonly ICommandExecutionService _executionService;
    private readonly IRemoteDefinitionService _definitionService;
    private readonly ILogger<ConversationController> _logger;
    private readonly Models.Conversation _viewModel;

    private readonly CancellationTokenSource _stop = new();

    public ConversationController(
        IOptionsSnapshot<ConversationSettings> options,
        ISpeechRecognition speechRecognition,
        ISpeechSynthesis speechSynthesis,
        IRemoteDefinitionService definitionService,
        ICommandExecutionService executionService,
        ILogger<ConversationController> logger,
        Models.Conversation viewModel)
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

    public void StartListening()
    {
        IReadOnlyDictionary<string, Command>? commands = GetCommands();

        if (commands is not null)
        {
            _ = ListenWithRetriesAsync(commands, _stop.Token);
        }
    }

    public void Dispose()
    {
        _logger.LogInformation(Message.ConversationController_Stopping);
        _stop.Cancel();
    }

    private IReadOnlyDictionary<string, Command>? GetCommands()
    {
        Dictionary<string, Command> commands = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (Command command in _definitionService.GetCommands())
            {
                commands[command.Name] = command;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(Message.ConversationController_ErrorDuringStartup, ex);
            _viewModel.StatusMessage = Phrases.Speech_ListeningSystemFailed;

            return null;
        }

        return commands;
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
                _viewModel.StatusMessage = string.Empty;
                _viewModel.IsListening = false;

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
                    _viewModel.StatusMessage = Phrases.Speech_ListeningSystemFailed;
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
        const string CommandPrefix = "Command:";

        while (true)
        {
            await ListenForAttentionAsync(cancellationToken);

            await foreach (IRecognitionResult result in ListenForCommandsAsync(cancellationToken))
            {
                _logger.LogInformation(Message.ConversationController_Recognized, result.Text, result.SemanticMeaning);

                if (result.SemanticMeaning.StartsWith(CommandPrefix) &&
                    commands.TryGetValue(result.SemanticMeaning.Substring(CommandPrefix.Length), out Command? command))
                {
                    await ExecuteCommandAsync(command, cancellationToken);
                }
                else
                {
                    _logger.LogError(Message.ConversationController_UnknownCommand, result.Text);
                }
            }
        }
    }

    private async Task ListenForAttentionAsync(CancellationToken cancellationToken)
    {
        _viewModel.StatusMessage = Phrases.Speech_ListeningForAttention;
        _logger.LogInformation(Message.ConversationController_ListenForAttention);

        await _speechRecognition.ListenForAttentionAsync(cancellationToken);
    }

    private async IAsyncEnumerable<IRecognitionResult> ListenForCommandsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            _viewModel.IsListening = true;
            _viewModel.StatusMessage = Phrases.Speech_ImListening;
            _speechSynthesis.Say(Phrases.Speech_ImListening);
            _logger.LogInformation(Message.ConversationController_ListenForCommands);

            await foreach (IRecognitionResult result in _speechRecognition.ListenForCommandsAsync(cancellationToken))
            {
                yield return result;

                _viewModel.IsListening = true;
                _viewModel.StatusMessage = Phrases.Speech_ImListening;
            }

            _speechSynthesis.Say(Phrases.Speech_StoppedListening);
        }
        finally
        {
            _viewModel.IsListening = false;
        }
    }

    private async Task ExecuteCommandAsync(Command command, CancellationToken cancellationToken)
    {
        _speechSynthesis.Say(Phrases.Speech_Sending(command.Name));
        _viewModel.StatusMessage = Phrases.Speech_ImSending;
        _logger.LogInformation(Message.ConversationController_Executing, command.Name);

        await _executionService.ExecuteAsync(command, cancellationToken);

        _logger.LogInformation(Message.ConversationController_Executed, command.Name);
    }
}
