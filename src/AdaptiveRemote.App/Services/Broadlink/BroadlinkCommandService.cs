using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdaptiveRemote.Services.Broadlink;

internal sealed class BroadlinkCommandService : CommandServiceBase<IRCommand>
{
    private readonly IDeviceLocator _deviceLocator;
    private readonly IDeviceConnection.Factory _connectionFactory;
    private readonly IOptionsSnapshot<IRDataSettings> _irDataSettings;
    private readonly IOptions<BroadlinkSettings> _broadlinkSettings;
    private readonly IPersistSettings _persistSettings;
    private readonly IModalMessageService _modalMessageService;

    /// <summary>
    /// Ensures that only one IR learning cycle runs at a time. A new request
    /// cancels any in-progress cycle before acquiring this semaphore.
    /// </summary>
    private readonly SemaphoreSlim _learningSemaphore = new(1, 1);

    /// <summary>
    /// CTS owned by the current semaphore holder. A new request exchanges this
    /// field (via <see cref="Interlocked.Exchange{T}"/>) to cancel the previous
    /// holder before waiting on the semaphore.
    /// </summary>
    private CancellationTokenSource? _currentLearnCts;

    private IDeviceConnection? _connection;

    public BroadlinkCommandService(
        IDeviceLocator deviceLocator,
        IDeviceConnection.Factory connectionFactory,
        IOptionsSnapshot<IRDataSettings> irDataSettings,
        IOptions<BroadlinkSettings> broadlinkSettings,
        IPersistSettings persistSettings,
        IRemoteDefinitionService definitionService,
        IModalMessageService modalMessageService,
        ILogger<BroadlinkCommandService> logger)
        : base("Broadlink IR Commands", definitionService, logger)
    {
        _deviceLocator = deviceLocator;
        _connectionFactory = connectionFactory;
        _irDataSettings = irDataSettings;
        _broadlinkSettings = broadlinkSettings;
        _persistSettings = persistSettings;
        _modalMessageService = modalMessageService;
    }

    public override async Task InitializeAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        activity.Description = Phrases.Startup_ConnectingToBroadlink;

        Logger.BroadlinkCommandService_SearchingForDevice();
        ScanResponsePacket found = await _deviceLocator.FindDeviceAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        _connection = _connectionFactory.Create(found.HostEndPoint, found.HostAddress, found.DeviceType);

        Logger.BroadlinkCommandService_Authenticating(found.HostEndPoint);
        await _connection.AuthenticateAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        Logger.BroadlinkCommandService_Authenticated(found.HostEndPoint);

        await base.InitializeAsync(activity, cancellationToken);

        Logger.BroadlinkCommandService_Ready();
    }

    protected override bool IsCommandEnabled(IRCommand command)
        => _irDataSettings.Value.ContainsKey(command.Name);

    protected override Command.ExecuteDelegate CreateHandler(IRCommand command)
    {
        if (!_irDataSettings.Value.TryGetValue(command.Name, out string? base64Data))
        {
            return _ => Task.FromException(
                new InvalidOperationException($"No IR data configured for command '{command.Name}'."));
        }

        byte[] data = Convert.FromBase64String(base64Data);
        return cancellationToken => _connection!.SendDataAsync(data, cancellationToken);
    }

    protected override Command.ExecuteDelegate? CreateProgramHandler(IRCommand command)
        => async cancellationToken =>
        {
            IDeviceConnection connection = _connection
                ?? throw new InvalidOperationException(Phrases.Broadlink_NotConnected(command.Name));

            string message = Phrases.Broadlink_ProgrammingCommand(command.Label);
            TimeSpan pollInterval = TimeSpan.FromSeconds(_broadlinkSettings.Value.LearnPollInterval);
            TimeSpan learnTimeout = TimeSpan.FromSeconds(_broadlinkSettings.Value.LearnTimeout);

            // Cancel any in-progress learning cycle, then wait for the semaphore it holds.
            using CancellationTokenSource learnCts = new();
            CancellationTokenSource? previousCts = Interlocked.Exchange(ref _currentLearnCts, learnCts);
            if (previousCts is not null)
            {
                Logger.BroadlinkCommandService_CancellingLearningForNewCommand(command);
                await previousCts.CancelAsync();
            }

            bool acquired = false;
            try
            {
                await _learningSemaphore.WaitAsync(cancellationToken);
                acquired = true;
                await _modalMessageService.ShowMessageAsync(message, async ct =>
                {
                    using CancellationTokenSource timeoutCts = new(learnTimeout);
                    using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token, learnCts.Token);

                    Logger.BroadlinkCommandService_EnteringLearningMode(command);
                    await connection.EnterLearningModeAsync(linkedCts.Token);

                    // Poll until data is received (early return), the user cancels (ct fires),
                    // a new button is clicked (learnCts fires), or the learning timeout fires.
                    try
                    {
                        while (true)
                        {
                            linkedCts.Token.ThrowIfCancellationRequested();

                            Logger.BroadlinkCommandService_PollingForLearnedData(command);
                            byte[]? data = await connection.CheckLearnedDataAsync(linkedCts.Token);

                            if (data is not null)
                            {
                                Logger.BroadlinkCommandService_LearnedDataReceived(command, data.Length);
                                string base64Data = Convert.ToBase64String(data);
                                _persistSettings.Set($"IRData:{command.Name}", base64Data);
                                command.ExecuteAsync = CreateWrappedHandler(command, sendCt => connection.SendDataAsync(data, sendCt));
                                command.IsEnabled = true;
                                return;
                            }

                            await Task.Delay(pollInterval, linkedCts.Token);
                        }
                    }
                    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested && !learnCts.Token.IsCancellationRequested)
                    {
                        Logger.BroadlinkCommandService_LearningTimedOut(command);
                        throw Errors.Broadlink_LearningTimedOut(learnTimeout);
                    }
                }, cancellationToken: cancellationToken);
            }
            finally
            {
                Interlocked.CompareExchange(ref _currentLearnCts, null, learnCts);
                if (acquired)
                {
                    _learningSemaphore.Release();
                }
            }
        };
}
