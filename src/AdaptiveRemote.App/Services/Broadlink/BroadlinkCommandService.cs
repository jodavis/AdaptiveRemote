using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Broadlink;

internal sealed class BroadlinkCommandService : CommandServiceBase<IRCommand>
{
    private const string IrDataKeyPrefix = "IRData:";

    private readonly IDeviceLocator _deviceLocator;
    private readonly IDeviceConnection.Factory _connectionFactory;
    private readonly IPersistSettings _persistSettings;

    private IDeviceConnection? _connection;

    public BroadlinkCommandService(
        IDeviceLocator deviceLocator,
        IDeviceConnection.Factory connectionFactory,
        IPersistSettings persistSettings,
        IRemoteDefinitionService definitionService,
        ILogger<BroadlinkCommandService> logger)
        : base("Broadlink IR Commands", definitionService, logger)
    {
        _deviceLocator = deviceLocator;
        _connectionFactory = connectionFactory;
        _persistSettings = persistSettings;
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

        foreach (IRCommand command in Commands)
        {
            string? base64Data = await _persistSettings.TryGetAsync($"{IrDataKeyPrefix}{command.Name}", cancellationToken);
            if (base64Data is not null)
            {
                byte[] data = Convert.FromBase64String(base64Data);
                command.ExecuteAsync = CreateWrappedHandler(command, ct => _connection.SendDataAsync(data, ct));
                command.IsEnabled = true;
            }
        }

        Logger.BroadlinkCommandService_Ready();
    }

    protected override Command.ExecuteDelegate CreateHandler(IRCommand command)
        => throw new NotSupportedException($"{nameof(BroadlinkCommandService)} configures handlers from programmatic settings during {nameof(InitializeAsync)}. This method is not called.");
}
