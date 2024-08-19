using System.Runtime.CompilerServices;
using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Conversation;

internal class ConversationController : ScopedBackgroundProcess
{
    private readonly ConversationSettings _speechSettings;
    private readonly ISpeechRecognition _speechRecognition;
    private readonly ISpeechSynthesis _speechSynthesis;
    private readonly IRemoteDefinitionService _definitionService;
    private readonly ConversationView _viewModel;

    private readonly CancellationTokenSource _stop = new();

    public ConversationController(
        IOptionsSnapshot<ConversationSettings> options,
        ISpeechRecognition speechRecognition,
        ISpeechSynthesis speechSynthesis,
        IRemoteDefinitionService definitionService,
        ILogger<ConversationController> logger,
        ConversationView viewModel)
        : base("Conversation system", logger)
    {
        _speechSettings = options.Value;
        _speechRecognition = speechRecognition;
        _speechSynthesis = speechSynthesis;
        _definitionService = definitionService;
        _viewModel = viewModel;

        _viewModel.IsListening = false;
        _viewModel.StatusMessage = Phrases.Conversation_WaitingForActivation;
    }

    public override Task CleanUpAsync(CancellationToken cancellationToken)
    {
        _viewModel.StatusMessage = "Shutting down...";

        return base.CleanUpAsync(cancellationToken);
    }

    private IReadOnlyDictionary<string, Command> GetCommands()
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
        catch
        {
            _viewModel.StatusMessage = Phrases.Conversation_SystemFailed;
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, Command> commands = GetCommands();

        int errorCount = 0;
        while (true)
        {
            _viewModel.ToggleListening = _speechRecognition.ToggleListening;

            try
            {
                await ListenAsync(commands, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                errorCount++;
                if (errorCount >= _speechSettings.ErrorRetryLimit)
                {
                    Logger.LogWarning(Message.ConversationController_RetryLimitReached, errorCount);
                    _viewModel.StatusMessage = Phrases.Conversation_SystemFailed;
                    throw;
                }
                else
                {
                    Logger.LogWarning(Message.ConversationController_Retrying, errorCount, ex);
                }
            }
            finally
            {
                _viewModel.ToggleListening = null;
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
                    Logger.LogInformation(Message.ConversationController_Recognized, result.Text, commandName);

                    if (commands.TryGetValue(commandName, out Command? command))
                    {
                        int repeat = ParseRepeat(result);

                        await ExecuteCommandAsync(command, repeat, cancellationToken);
                    }
                    else
                    {
                        Logger.LogError(Message.ConversationController_UnknownCommand, result.Text);
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
            Logger.LogInformation(Message.ConversationController_ListenForAttention);

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
            Logger.LogInformation(Message.ConversationController_ListenForCommands);

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
        Command.ExecuteDelegate? executeAsync = command.ExecuteAsync;
        if (executeAsync is null)
        {
            Logger.LogError(Message.ConversationController_CommandMissingExecuteAction, command.Name);
            await SayAsync(Phrases.Conversation_CommandDisabled(command.Name), cancellationToken);
        }
        else if (!command.IsEnabled)
        {
            Logger.LogError(Message.ConversationController_CommandDisabled, command.Name);
            await SayAsync(Phrases.Conversation_CommandDisabled(command.Name), cancellationToken);
        }
        else
        {
            Task sayTask = SayAsync(Phrases.Conversation_Sent(command.Name, repeat), cancellationToken);

            string previousMessage = _viewModel.StatusMessage;
            _viewModel.StatusMessage = Phrases.Conversation_ImSending;

            try
            {
                for (int i = 0; i < repeat; i++)
                {
                    Logger.LogInformation(Message.ConversationController_Executing, command.Name);

                    await executeAsync(cancellationToken);

                    Logger.LogInformation(Message.ConversationController_Executed, command.Name);
                }

                await sayTask;
            }
            finally
            {
                _viewModel.StatusMessage = previousMessage;
            }
        }
    }

    private async Task SayAsync(string phrase, CancellationToken cancellationToken)
    {
        bool wasListening = _viewModel.IsListening;
        try
        {
            _viewModel.IsListening = false;
            _viewModel.SpeakingMessage = phrase;
            await _speechSynthesis.SayAsync(phrase, default);
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            _viewModel.IsListening = wasListening;
            _viewModel.SpeakingMessage = null;
        }
    }
}
