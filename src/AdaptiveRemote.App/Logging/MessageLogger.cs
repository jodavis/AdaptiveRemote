using System.Net;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Logging;

internal partial class MessageLogger
{
    private readonly ILogger _logger;

    public MessageLogger(ILogger logger)
    {
        _logger = logger;
    }

    [LoggerMessage(EventId = 701, Level = LogLevel.Information, Message = "Initializing {ServiceName}")]
    public partial void ApplicationLifecycle_Initializing(string serviceName);

    [LoggerMessage(EventId = 702, Level = LogLevel.Information, Message = "Initialized {ServiceName}")]
    public partial void ApplicationLifecycle_Initialized(string serviceName);

    [LoggerMessage(EventId = 703, Level = LogLevel.Error, Message = "Error while initializing {ServiceName}")]
    public partial void ApplicationLifecycle_InitializingFailed(string serviceName, Exception error);

    [LoggerMessage(EventId = 704, Level = LogLevel.Information, Message = "Cleaning up {ServiceName}")]
    public partial void ApplicationLifecycle_CleaningUp(string serviceName);

    [LoggerMessage(EventId = 705, Level = LogLevel.Information, Message = "Cleaned up {ServiceName}")]
    public partial void ApplicationLifecycle_CleanedUp(string serviceName);

    [LoggerMessage(EventId = 706, Level = LogLevel.Error, Message = "Error while cleaning up {ServiceName}")]
    public partial void ApplicationLifecycle_CleaningUpFailed(string serviceName, Exception error);

    [LoggerMessage(EventId = 707, Level = LogLevel.Information, Message = "Shutting down")]
    public partial void ApplicationLifecycle_ShuttingDown();

    [LoggerMessage(EventId = 708, Level = LogLevel.Error, Message = "An error occurred while constructing the ScopedLifecycleContainer. Either there is a missing dependency, or a dependency's constructor threw an exception.")]
    public partial void ApplicationLifecycle_ScopeConstructionFailed(Exception error);

    [LoggerMessage(EventId = 709, Level = LogLevel.Error, Message = "An unhandled error occurred in the application lifecycle")]
    public partial void ApplicationLifecycle_UnhandledError(Exception error);

    [LoggerMessage(EventId = 710, Level = LogLevel.Information, Message = "Waiting for application scope")]
    public partial void ApplicationLifecycle_WaitingForScope();

    [LoggerMessage(EventId = 711, Level = LogLevel.Information, Message = "Application scope released, shutting down")]
    public partial void ApplicationLifecycle_ScopeReleased();

    [LoggerMessage(EventId = 712, Level = LogLevel.Information, Message = "Recycling application scope")]
    public partial void ApplicationLifecycle_RecyclingScope();

    [LoggerMessage(EventId = 205, Level = LogLevel.Warning, Message = "Not restarting after {ErrorCount} error(s)")]
    public partial void ConversationController_RetryLimitReached(int errorCount);

    [LoggerMessage(EventId = 206, Level = LogLevel.Warning, Message = "Restarting after {ErrorCount} error(s) in speech recognition loop")]
    public partial void ConversationController_Retrying(int errorCount, Exception error);

    [LoggerMessage(EventId = 207, Level = LogLevel.Information, Message = "Recognized '{Result}' as '{Command}'")]
    public partial void ConversationController_Recognized(string result, string command);

    [LoggerMessage(EventId = 208, Level = LogLevel.Warning, Message = "Recognized an unknown command: {Command}")]
    public partial void ConversationController_UnknownCommand(string command);

    [LoggerMessage(EventId = 210, Level = LogLevel.Information, Message = "Executing command '{Command}'")]
    public partial void ConversationController_Executing(string command);

    [LoggerMessage(EventId = 211, Level = LogLevel.Information, Message = "Executed command '{Command}'")]
    public partial void ConversationController_Executed(string command);

    [LoggerMessage(EventId = 213, Level = LogLevel.Error, Message = "Could not execute {Command} because it is missing its execute action")]
    public partial void ConversationController_CommandMissingExecuteAction(Models.Command? command);

    [LoggerMessage(EventId = 214, Level = LogLevel.Error, Message = "Could not execute {Command} because it is not enabled")]
    public partial void ConversationController_CommandDisabled(Models.Command? command);

    [LoggerMessage(EventId = 501, Level = LogLevel.Information, Message = "Listening={Listening} ({ListenCount} listen, {PauseCount} pause)")]
    public partial void ListeningController_State(bool listening, int listenCount, int pauseCount);

    [LoggerMessage(EventId = 502, Level = LogLevel.Error, Message = "Error calling RecognizeAsync")]
    public partial void ListeningController_RecognizeAsyncError(Exception error);

    [LoggerMessage(EventId = 503, Level = LogLevel.Error, Message = "Error calling RecognizeAsyncCancel")]
    public partial void ListeningController_RecognizeAsyncCancelError(Exception error);

    [LoggerMessage(EventId = 504, Level = LogLevel.Error, Message = "Error while updating listen state")]
    public partial void ListeningController_UpdateListenStateError(Exception error);

    [LoggerMessage(EventId = 601, Level = LogLevel.Information, Message = "Executing {Command}")]
    public partial void CommandService_Executing(Models.Command command);

    [LoggerMessage(EventId = 602, Level = LogLevel.Information, Message = "Executed {Command}")]
    public partial void CommandService_Executed(Models.Command command);

    [LoggerMessage(EventId = 603, Level = LogLevel.Error, Message = "Error executing {Command}")]
    public partial void CommandService_Error(Models.Command command, Exception error);

    [LoggerMessage(EventId = 604, Level = LogLevel.Warning, Message = "Cancelled executing {Command}")]
    public partial void CommandService_Cancelled(Models.Command command);

    [LoggerMessage(EventId = 605, Level = LogLevel.Error, Message = "Could not execute {Command} because the service has not been started")]
    public partial void CommandService_NotStarted(Models.Command command);

    [LoggerMessage(EventId = 606, Level = LogLevel.Error, Message = "Could not execute {Command} because the service has been shut down")]
    public partial void CommandService_WasShutDown(Models.Command command);

    [LoggerMessage(EventId = 607, Level = LogLevel.Information, Message = "Programming {Command}")]
    public partial void CommandService_Programming(Models.Command command);

    [LoggerMessage(EventId = 608, Level = LogLevel.Information, Message = "Programmed {Command}")]
    public partial void CommandService_Programmed(Models.Command command);

    [LoggerMessage(EventId = 609, Level = LogLevel.Error, Message = "Error programming {Command}")]
    public partial void CommandService_ProgramError(Models.Command command, Exception error);

    [LoggerMessage(EventId = 610, Level = LogLevel.Warning, Message = "Cancelled programming {Command}")]
    public partial void CommandService_ProgramCancelled(Models.Command command);

    [LoggerMessage(EventId = 611, Level = LogLevel.Error, Message = "Could not program {Command} because the service has been shut down")]
    public partial void CommandService_ProgramWasShutDown(Models.Command command);

    [LoggerMessage(EventId = 801, Level = LogLevel.Information, Message = "Message sent: {Message}")]
    public partial void TiVoConnection_MessageSent(string message);

    [LoggerMessage(EventId = 802, Level = LogLevel.Information, Message = "Message received: {Message}")]
    public partial void TiVoConnection_MessageReceived(string message);

    [LoggerMessage(EventId = 803, Level = LogLevel.Information, Message = "Received event code \"{ReceivedEventCode}\" in response to \"{SentEventCode}\": \"{EventValue}\"")]
    public partial void TiVoConnection_EventReceived(string receivedEventCode, string sentEventCode, string eventValue);

    [LoggerMessage(EventId = 804, Level = LogLevel.Error, Message = "Error")]
    public partial void TiVoConnection_Error(Exception error);

    [LoggerMessage(EventId = 805, Level = LogLevel.Information, Message = "Connected to TiVo {Device}")]
    public partial void TiVoConnection_Connected(string device);

    [LoggerMessage(EventId = 806, Level = LogLevel.Information, Message = "Disconnecting from TiVo {Device}")]
    public partial void TiVoConnection_Disconnecting(string device);

    [LoggerMessage(EventId = 901, Level = LogLevel.Information, Message = "{MessageID}: Sending {ByteCount} bytes to {Device}")]
    public partial void UdpService_Sending(object messageId, int byteCount, System.Net.EndPoint device);

    [LoggerMessage(EventId = 902, Level = LogLevel.Information, Message = "{MessageID}: Sent, waiting for response")]
    public partial void UdpService_Sent(object messageId);

    [LoggerMessage(EventId = 903, Level = LogLevel.Information, Message = "{MessageID}: Received {ByteCount} bytes from {Device}")]
    public partial void UdpService_ReceivedResponse(object messageId, int byteCount, System.Net.EndPoint device);

    [LoggerMessage(EventId = 904, Level = LogLevel.Warning, Message = "{MessageID}: Cancelled")]
    public partial void UdpService_Cancelled(object messageId);

    [LoggerMessage(EventId = 905, Level = LogLevel.Error, Message = "{MessageID}: Failed")]
    public partial void UdpService_Failed(object messageId, Exception error);

    [LoggerMessage(EventId = 906, Level = LogLevel.Error, Message = "{MessageID}: Unexpected error")]
    public partial void UdpService_UnexpectedError(object messageId, Exception error);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Searching for Broadlink devices...")]
    public partial void BroadlinkCommandService_SearchingForDevice();

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Authenticating with device at {Address}")]
    public partial void BroadlinkCommandService_Authenticating(EndPoint address);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Authenticated successfully with device at {Address}")]
    public partial void BroadlinkCommandService_Authenticated(EndPoint address);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "Ready to send commands")]
    public partial void BroadlinkCommandService_Ready();

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information, Message = "Entering IR learning mode for {Command}")]
    public partial void BroadlinkCommandService_EnteringLearningMode(Models.Command command);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Information, Message = "Polling for learned IR data for {Command}")]
    public partial void BroadlinkCommandService_PollingForLearnedData(Models.Command command);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Information, Message = "Learned IR data received for {Command} ({ByteCount} bytes)")]
    public partial void BroadlinkCommandService_LearnedDataReceived(Models.Command command, int byteCount);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Warning, Message = "IR learning timed out for {Command}")]
    public partial void BroadlinkCommandService_LearningTimedOut(Models.Command command);

    [LoggerMessage(EventId = 1009, Level = LogLevel.Warning, Message = "IR learning already in progress; ignoring request to program {Command}")]
    public partial void BroadlinkCommandService_LearningAlreadyInProgress(Models.Command command);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "Loading existing settings from {FilePath}")]
    public partial void ProgrammaticSettings_LoadingExistingSettings(string filePath);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Information, Message = "Loaded {Count} settings from {FilePath}")]
    public partial void ProgrammaticSettings_LoadedExistingSettings(int count, string filePath);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Information, Message = "Saving {Count} settings to {FilePath}")]
    public partial void ProgrammaticSettings_SavingSettings(int count, string filePath);

    [LoggerMessage(EventId = 1104, Level = LogLevel.Information, Message = "Saved settings to {FilePath}")]
    public partial void ProgrammaticSettings_SavedSettings(string filePath);

    [LoggerMessage(EventId = 1105, Level = LogLevel.Information, Message = "Setting {Name}={Value}")]
    public partial void ProgrammaticSettings_AddSetting(string name, string value);

    [LoggerMessage(EventId = 1106, Level = LogLevel.Information, Message = "Replacing {Name}={OldValue} with {Name}={NewValue}")]
    public partial void ProgrammaticSettings_ReplaceSetting(string name, string oldValue, string newValue);

    [LoggerMessage(EventId = 1107, Level = LogLevel.Error, Message = "Setting {Name}={Value} was rejected: {Reason}")]
    public partial void ProgrammaticSettings_Rejected(string name, string value, string reason);

    [LoggerMessage(EventId = 1108, Level = LogLevel.Error, Message = "Failed to set {Name}={Value}")]
    public partial void ProgrammaticSettings_Error(string name, string value, Exception error);

    [LoggerMessage(EventId = 1201, Level = LogLevel.Information, Message = "Starting")]
    public partial void ScopedBackgroundProcess_Starting();

    [LoggerMessage(EventId = 1202, Level = LogLevel.Information, Message = "Started")]
    public partial void ScopedBackgroundProcess_Started();

    [LoggerMessage(EventId = 1203, Level = LogLevel.Information, Message = "Stopping")]
    public partial void ScopedBackgroundProcess_Stopping();

    [LoggerMessage(EventId = 1204, Level = LogLevel.Information, Message = "Stopped")]
    public partial void ScopedBackgroundProcess_Stopped();

    [LoggerMessage(EventId = 1205, Level = LogLevel.Warning, Message = "Stopped early")]
    public partial void ScopedBackgroundProcess_StoppedEarly();

    [LoggerMessage(EventId = 1206, Level = LogLevel.Error, Message = "Terminated due to error")]
    public partial void ScopedBackgroundProcess_Error(Exception error);

    [LoggerMessage(EventId = 1207, Level = LogLevel.Warning, Message = "Startup canceled")]
    public partial void ScopedBackgroundProcess_CanceledBeforeStarted();

    [LoggerMessage(EventId = 1208, Level = LogLevel.Debug, Message = "Switched to worker thread {ThreadId}")]
    public partial void ScopedBackgroundProcess_SwitchedToWorkerThread(int threadId);

    [LoggerMessage(EventId = 1209, Level = LogLevel.Debug, Message = "Switching to worker thread")]
    public partial void ScopedBackgroundProcess_SwitchingToWorkerThread();

    [LoggerMessage(EventId = 1301, Level = LogLevel.Information, Message = "{NewState}")]
    public partial void ConversationState_Updated(Services.Conversation.ConversationState newState);

    [LoggerMessage(EventId = 1302, Level = LogLevel.Error, Message = "Did not expect {PhraseKind} but received \"{RecognizedSpeech}\"")]
    public partial void ConversationState_UnexpectedSpeechDetected(Services.Conversation.PhraseKinds phraseKind, Services.Conversation.IRecognizedSpeech recognizedSpeech);

    [LoggerMessage(EventId = 1303, Level = LogLevel.Warning, Message = "Received an invalid semantic value for '{SemanticKey}': {SemanticValue}")]
    public partial void ConversationState_InvalidSemanticValue(string semanticKey, string semanticValue);

    [LoggerMessage(EventId = 1304, Level = LogLevel.Error, Message = "User reported recognition error: {Speech}")]
    public partial void ConversationState_UserReportedRecognitionError(Services.Conversation.IRecognizedSpeech speech);

    [LoggerMessage(EventId = 1305, Level = LogLevel.Error, Message = "Could not reverse {Command}. Could not find a command named '{ReverseCommand}'.")]
    public partial void ConversationState_CouldNotFindReverseCommand(Models.Command command, string? reverseCommand);

    // SpeechRecognition (non-Engine) messages
    [LoggerMessage(EventId = 302, Level = LogLevel.Error, Message = "Recognition Error")]
    public partial void SpeechRecognition_RecognitionError(Exception error);

    [LoggerMessage(EventId = 303, Level = LogLevel.Information, Message = "{ListeningMethod} was cancelled.")]
    public partial void SpeechRecognition_CancelledListeningMethod(string listeningMethod);

    [LoggerMessage(EventId = 304, Level = LogLevel.Error, Message = "An error occurred in {ListeningMethod}")]
    public partial void SpeechRecognition_ErrorInListeningMethod(string listeningMethod, Exception error);

    [LoggerMessage(EventId = 305, Level = LogLevel.Information, Message = "{GrammarName}.Enabled={Enabled}")]
    public partial void SpeechRecognition_GrammarEnabled(string grammarName, bool enabled);

    [LoggerMessage(EventId = 308, Level = LogLevel.Warning, Message = "{ListeningMethod} is already in progress")]
    public partial void SpeechRecognition_ListeningMethodAlreadyInProgress(string listeningMethod);

    [LoggerMessage(EventId = 309, Level = LogLevel.Error, Message = "Failed to load '{GrammarName}'")]
    public partial void SpeechRecognition_GrammarFailedToLoad(string grammarName, Exception? error);

    [LoggerMessage(EventId = 310, Level = LogLevel.Error, Message = "Failed to unload '{GrammarName}'")]
    public partial void SpeechRecognition_GrammarFailedToUnload(string grammarName, Exception? error);

    [LoggerMessage(EventId = 311, Level = LogLevel.Warning, Message = "Could not set '{RecognizerSetting}': {ErrorMessage}")]
    public partial void SpeechRecognition_CouldNotConfigureSetting(string recognizerSetting, string errorMessage);

    // SpeechSynthesis messages
    [LoggerMessage(EventId = 401, Level = LogLevel.Information, Message = "Selected voice '{VoiceName}'")]
    public partial void SpeechSynthesis_SelectedVoice(string voiceName);

    [LoggerMessage(EventId = 402, Level = LogLevel.Warning, Message = "Voice not found: '{VoiceName}'")]
    public partial void SpeechSynthesis_VoiceNotFound(string voiceName);

    [LoggerMessage(EventId = 403, Level = LogLevel.Information, Message = "Saying '{Phrase}'")]
    public partial void SpeechSynthesis_Saying(string phrase);

    [LoggerMessage(EventId = 404, Level = LogLevel.Information, Message = "Cancelled saying '{Phrase}'")]
    public partial void SpeechSynthesis_CancelledSaying(string phrase);

    [LoggerMessage(EventId = 405, Level = LogLevel.Warning, Message = "Cannot say '{Phrase}'; another phrase is already in progress.")]
    public partial void SpeechSynthesis_AlreadySpeaking(string phrase);

    [LoggerMessage(EventId = 406, Level = LogLevel.Information, Message = "Set speaking rate to {SpeakingRate}")]
    public partial void SpeechSynthesis_SetSpeakingRate(int speakingRate);

    [LoggerMessage(EventId = 1401, Level = LogLevel.Information, Message = "Started listening for speech samples")]
    public partial void SamplesRecorder_StartListening();

    [LoggerMessage(EventId = 1402, Level = LogLevel.Information, Message = "Stopped listening for speech samples")]
    public partial void SamplesRecorder_StopListening();

    [LoggerMessage(EventId = 1403, Level = LogLevel.Information, Message = "Saved wave file to {WavPath}")]
    public partial void SamplesRecorder_SavedWaveFile(string wavPath);

    [LoggerMessage(EventId = 1404, Level = LogLevel.Information, Message = "Saved log file to {LogPath}")]
    public partial void SamplesRecorder_SavedLogFile(string logPath);

    [LoggerMessage(EventId = 1501, Level = LogLevel.Information, Message = "Starting Test Control Endpoint on port {Port}")]
    public partial void TestEndpointService_StartingTestControlEndpoint(int port);

    [LoggerMessage(EventId = 1502, Level = LogLevel.Information, Message = "Stopping Test Control Endpoint")]
    public partial void TestEndpointService_StopTestControlEndpoint();

    [LoggerMessage(EventId = 1503, Level = LogLevel.Error, Message = "Failed to start Test Control Endpoint")]
    public partial void TestEndpointService_StartingTestControlEndpointFailed(Exception error);

    [LoggerMessage(EventId = 1504, Level = LogLevel.Information, Message = "Client connected to Test Control Endpoint")]
    public partial void TestEndpointService_ClientConnected();

    [LoggerMessage(EventId = 1505, Level = LogLevel.Error, Message = "Client connection to Test Control Endpoint failed")]
    public partial void TestEndpointService_ClientConnectionFailed(Exception error);

    [LoggerMessage(EventId = 1506, Level = LogLevel.Information, Message = "Loading test service {TypeName} from {AssemblyPath}")]
    public partial void TestEndpointService_LoadingTestService(string typeName, string assemblyPath);

    [LoggerMessage(EventId = 1507, Level = LogLevel.Information, Message = "Successfully loaded test service")]
    public partial void TestEndpointService_LoadingTestServiceSucceeded();

    [LoggerMessage(EventId = 1508, Level = LogLevel.Error, Message = "Test service {TypeName} does not implement {ServiceContract}")]
    public partial void TestEndpointService_ServiceTypeIncompatible(string typeName, string? serviceContract);

    [LoggerMessage(EventId = 1509, Level = LogLevel.Information, Message = "Registering test service {ServiceName} for contract {ContractType}")]
    public partial void TestEndpointHooksService_RegisteringTestService(string serviceName, string contractType);

    [LoggerMessage(EventId = 1510, Level = LogLevel.Information, Message = "Signaling host to build")]
    public partial void TestEndpointHooksService_SignalingBuildHost();

    [LoggerMessage(EventId = 1511, Level = LogLevel.Information, Message = "Signaling host to abort startup")]
    public partial void TestEndpointHooksService_SignalingAbort();

    [LoggerMessage(EventId = 1512, Level = LogLevel.Information, Message = "Waiting for test services to be registered")]
    public partial void TestEndpointHooksService_WaitingForTestServices();

    [LoggerMessage(EventId = 1513, Level = LogLevel.Information, Message = "Registered {Count} test service(s)")]
    public partial void TestEndpointHooksService_TestServicesRegistered(int count);

    [LoggerMessage(EventId = 1514, Level = LogLevel.Information, Message = "Providing services to test")]
    public partial void TestEndpointHooksService_ProvidingServicesToTest();

    [LoggerMessage(EventId = 1515, Level = LogLevel.Information, Message = "Loading test service type {ServiceName} from {AssemblyPath}")]
    public partial void TestEndpointHooksService_LoadingTestServiceType(string serviceName, string assemblyPath);

    [LoggerMessage(EventId = 1516, Level = LogLevel.Information, Message = "Registering test service {ServiceName} implementing {ContractType} in DI container")]
    public partial void TestEndpointHooksService_RegisteringTestServiceInDI(string serviceName, string contractType);

    // 1600–1699: CognitoTokenService

    // 1700–1799: CloudAssetOrchestrator

    [LoggerMessage(EventId = 1700, Level = LogLevel.Information, Message = "Downloading asset '{AssetName}'")]
    public partial void CloudAssetOrchestrator_Downloading(string assetName);

    [LoggerMessage(EventId = 1701, Level = LogLevel.Error, Message = "Failed to initialize cloud assets")]
    public partial void CloudAssetOrchestrator_Failed(Exception error);

    [LoggerMessage(EventId = 1600, Level = LogLevel.Information, Message = "Acquiring Cognito access token via Client Credentials flow")]
    public partial void CognitoTokenService_AcquiringToken();

    [LoggerMessage(EventId = 1601, Level = LogLevel.Information, Message = "Cognito access token acquired successfully")]
    public partial void CognitoTokenService_TokenAcquired();

    [LoggerMessage(EventId = 1602, Level = LogLevel.Error, Message = "Failed to acquire Cognito access token")]
    public partial void CognitoTokenService_AcquireTokenFailed(Exception exception);
}
