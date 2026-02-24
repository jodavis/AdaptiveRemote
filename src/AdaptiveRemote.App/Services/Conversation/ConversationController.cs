using AdaptiveRemote.Models;
using AdaptiveRemote.Services.ModalMessages;
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
    private readonly IModalMessageService _modalMessageService;

    public ConversationController(
        IOptionsSnapshot<ConversationSettings> options,
        ISpeechRecognition speechRecognition,
        ISpeechSynthesis speechSynthesis,
        ILogger<ConversationController> logger,
        ConversationStateMachine stateMachine,
        ConversationView viewModel,
        IModalMessageService modalMessageService)
        : base("Conversation system", logger)
    {
        _speechSettings = options.Value;
        _speechRecognition = speechRecognition;
        _speechSynthesis = speechSynthesis;
        _stateMachine = stateMachine;
        _viewModel = viewModel;
        _modalMessageService = modalMessageService;

        _viewModel.IsListening = false;
        _viewModel.StatusMessage = Phrases.Conversation_WaitingForActivation;
    }

    public override async Task CleanUpAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        try
        {
            _viewModel.StatusMessage = Phrases.Cleanup_ShuttingDown;

            await base.CleanUpAsync(activity, cancellationToken);
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
            _viewModel.ToggleListening = ToggleListening;

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
                    Logger.ConversationController_RetryLimitReached(errorCount);
                    _viewModel.StatusMessage = Phrases.Conversation_SystemFailed;
                    throw;
                }
                else
                {
                    Logger.ConversationController_Retrying(errorCount, ex);
                }
            }
            finally
            {
                _viewModel.IsListening = false;
                _viewModel.ToggleListening = null;
            }
        }
    }

    private void ToggleListening()
    {
        _stateMachine.ToggleListening();
        UpdateViewModelAndRecognition();
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
        bool isAsking = _stateMachine.WantPhrases.HasFlag(PhraseKinds.Confirmation);

        Task commandTask = ExecuteCommandsAsync(response.Commands, cancellationToken);
        Task speakingTask = SayAsync(response.Phrases, isAsking);

        await speakingTask;
        await commandTask;
    }

    private async Task ExecuteCommandsAsync(IEnumerable<Command> commands, CancellationToken cancellationToken)
    {
        foreach (Command command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Logger.ConversationController_Executing(command.Name);

            await command.ExecuteAsync!(cancellationToken);

            Logger.ConversationController_Executed(command.Name);
        }
    }

    private async Task SayAsync(IEnumerable<string> phrases, bool isAsking)
    {
        bool wasListening = _viewModel.IsListening;
        try
        {
            _viewModel.IsListening = false;
            string[] phraseList = phrases.ToArray();
            for (int i = 0; i < phraseList.Length; i++)
            {
                string phrase = phraseList[i];
                bool keepAlive = isAsking && i == phraseList.Length - 1;
                await _modalMessageService.ShowMessageAsync(phrase, ct => _speechSynthesis.SayAsync(phrase, ct), keepAlive);
            }
        }
        finally
        {
            _viewModel.IsListening = wasListening;
        }
    }
}
