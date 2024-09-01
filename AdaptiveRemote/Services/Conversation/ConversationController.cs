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
            _viewModel.ToggleListening = _stateMachine.ToggleListening;

            try
            {
                await ListenAsync(cancellationToken);

                _viewModel.StatusMessage = string.Empty;
                break;
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
                _viewModel.IsListening = false;
                _viewModel.ToggleListening = null;
            }
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        _stateMachine.Reset();

        UpdateViewModelAndRecognition();

        await foreach (IRecognizedSpeech speech in _speechRecognition.RecognizeAsync(cancellationToken))
        {
            ConversationResponse response = _stateMachine.RespondTo(speech);

            UpdateViewModelAndRecognition(sending: response.Commands.Any());

            await ExecuteResponseAsync(response, cancellationToken);

            UpdateViewModelAndRecognition(sending: false);
        }
    }

    private void UpdateViewModelAndRecognition(bool sending = false)
    {
        _speechRecognition.SetFilter(_stateMachine.WantPhrases);

        if (_stateMachine.WantPhrases == PhraseKinds.WakeWord)
        {
            _viewModel.IsListening = false;
            _viewModel.StatusMessage = Phrases.Conversation_ListeningForAttention;
        }
        else
        {
            _viewModel.IsListening = true;
            _viewModel.StatusMessage = sending
                ? Phrases.Conversation_ImSending
                : Phrases.Conversation_ImListening;
        }
    }

    private async Task ExecuteResponseAsync(ConversationResponse response, CancellationToken cancellationToken)
    {
        Task commandTask = ExecuteCommandsAsync(response.Commands, cancellationToken);
        Task speakingTask = SayAsync(response.Phrases, cancellationToken);

        await speakingTask;
        await commandTask;
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
        bool wasListening = _viewModel.IsListening;
        try
        {
            _viewModel.IsListening = false;
            foreach (string phrase in phrases)
            {
                _viewModel.SpeakingMessage = phrase;
                await _speechSynthesis.SayAsync(phrase, default);
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            _viewModel.IsListening = wasListening;
            _viewModel.SpeakingMessage = null;
        }
    }
}
