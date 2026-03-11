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
        => cancellationToken =>
        {
            IDeviceConnection connection = _connection
                ?? throw new InvalidOperationException($"Cannot program {command}: the Broadlink service is not connected.");

            string message = Phrases.Broadlink_ProgrammingCommand(command.Label);
            TimeSpan pollInterval = TimeSpan.FromSeconds(_broadlinkSettings.Value.LearnPollInterval);

            return _modalMessageService.ShowMessageAsync(message, async ct =>
            {
                Logger.BroadlinkCommandService_EnteringLearningMode(command);
                await connection.EnterLearningModeAsync(ct);

                // Poll indefinitely: the loop exits when data is received (early return),
                // the cancellation token fires (OperationCanceledException), or the device
                // times out in learning mode and throws a BroadlinkException.
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    Logger.BroadlinkCommandService_PollingForLearnedData(command);
                    byte[]? data = await connection.CheckLearnedDataAsync(ct);

                    if (data is not null)
                    {
                        Logger.BroadlinkCommandService_LearnedDataReceived(command, data.Length);
                        string base64Data = Convert.ToBase64String(data);
                        _persistSettings.Set($"IRData:{command.Name}", base64Data);
                        command.ExecuteAsync = CreateWrappedHandler(command, sendCt => connection.SendDataAsync(data, sendCt));
                        command.IsEnabled = true;
                        return;
                    }

                    await Task.Delay(pollInterval, ct);
                }
            }, cancellationToken: cancellationToken);
        };
}
