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
    private readonly ConversationStateMachine _stateMachine;
    private readonly ConversationView _viewModel;

    private readonly CancellationTokenSource _stop = new();

    public ConversationController(
        IOptionsSnapshot<ConversationSettings> options,
        ISpeechRecognition speechRecognition,
        ISpeechSynthesis speechSynthesis,
        ILogger<ConversationController> logger,
        ConversationStateMachine stateMachine,
        ConversationView viewModel)
        : base("Conversation system", logger)
    {
        _speechSettings = options.Value;
        _speechRecognition = speechRecognition;
        _speechSynthesis = speechSynthesis;
        _stateMachine = stateMachine;
        _viewModel = viewModel;

        _viewModel.IsListening = false;
        _viewModel.StatusMessage = Phrases.Conversation_WaitingForActivation;
    }

    public override Task CleanUpAsync(CancellationToken cancellationToken)
    {
        _viewModel.StatusMessage = "Shutting down...";

        return base.CleanUpAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        int errorCount = 0;
        while (true)
        {
            _viewModel.ToggleListening = _speechRecognition.ToggleListening;

            try
            {
                await ListenAsync(cancellationToken);
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

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            await ListenForAttentionAsync(cancellationToken);

            await foreach (IRecognitionResult result in ListenForCommandsAsync(cancellationToken))
            {
                string previousMessage = _viewModel.StatusMessage;
                _viewModel.StatusMessage = Phrases.Conversation_ImSending;

                try
                {
                    ConversationResponse response = _stateMachine.RespondTo(result);

                    Task speakingTask = SayAsync(response.Phrases, cancellationToken);
                    Task commandTask = ExecuteCommandsAsync(response.Commands, cancellationToken);

                    await Task.WhenAll(speakingTask, commandTask);
                }
                finally
                {
                    _viewModel.StatusMessage = previousMessage;
                }
            }
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

    private async Task ExecuteCommandsAsync(IEnumerable<Command> commands, CancellationToken cancellationToken)
    {
        foreach (Command command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteCommandAsync(command, 1, cancellationToken);
        }
    }

    private async Task ExecuteCommandAsync(Command command, int repeat, CancellationToken cancellationToken)
    {
        for (int i = 0; i < repeat; i++)
        {
            Logger.LogInformation(Message.ConversationController_Executing, command.Name);

            await command.ExecuteAsync!(cancellationToken);

            Logger.LogInformation(Message.ConversationController_Executed, command.Name);
        }
    }

    private async Task SayAsync(IEnumerable<string> phrases, CancellationToken cancellationToken)
    {
        foreach (string phrase in phrases)
        {
            await SayAsync(phrase, cancellationToken);
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
