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

    public override async Task CleanUpAsync(CancellationToken cancellationToken)
    {
        try
        {
            _viewModel.StatusMessage = "Shutting down...";

            await base.CleanUpAsync(cancellationToken);
        }
        finally
        {
            _viewModel.StatusMessage = string.Empty;
        }
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
                _viewModel.StatusMessage = string.Empty;
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
                _stateMachine.IsListening = false;
                _viewModel.IsListening = false;
                _viewModel.ToggleListening = null;
            }
        }
    }

    private async IAsyncEnumerable<ConversationResponse> GetResponsesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            await _speechRecognition.ListenForAttentionAsync(cancellationToken);

            _stateMachine.IsListening = true;

            yield return new([Phrases.Conversation_ImListening], Array.Empty<Command>());

            await foreach (IRecognitionResult result in _speechRecognition.ListenForCommandsAsync(cancellationToken))
            {
                yield return _stateMachine.RespondTo(result);
            }

            _stateMachine.IsListening = false;

            yield return new([Phrases.Conversation_StoppedListening], Array.Empty<Command>());
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        _viewModel.StatusMessage = Phrases.Conversation_ListeningForAttention;
        Logger.LogInformation(Message.ConversationController_ListenForAttention);

        bool wasListening = _stateMachine.IsListening;

        await foreach (ConversationResponse response in GetResponsesAsync(cancellationToken))
        {
            if (_stateMachine.IsListening)
            {
                if (!wasListening)
                {
                    wasListening = true;
                    Logger.LogInformation(Message.ConversationController_ListenForCommands);
                }

                _viewModel.StatusMessage = Phrases.Conversation_ImListening;
                _viewModel.IsListening = true;

                if (response.Commands.Any())
                {
                    _viewModel.StatusMessage = Phrases.Conversation_ImSending;
                }

                Task commandTask = ExecuteCommandsAsync(response.Commands, cancellationToken);
                Task speakingTask = SayAsync(response.Phrases, cancellationToken);

                await speakingTask;
                await commandTask;

                _viewModel.StatusMessage = Phrases.Conversation_ImListening;
            }
            else
            {
                _viewModel.StatusMessage = Phrases.Conversation_ListeningForAttention;
                _viewModel.IsListening = false;

                await SayAsync(response.Phrases, cancellationToken);

                if (wasListening)
                {
                    wasListening = false;
                    Logger.LogInformation(Message.ConversationController_ListenForAttention);
                }
            }
        }
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
