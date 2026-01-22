using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Broadlink;

internal sealed class BroadlinkCommandService : CommandServiceBase<IRCommand>
{
    private readonly IDeviceLocator _deviceLocator;
    private readonly IDeviceConnection.Factory _connectionFactory;

    private IDeviceConnection? _connection;

    public BroadlinkCommandService(
        IDeviceLocator deviceLocator,
        IDeviceConnection.Factory connectionFactory,
        IRemoteDefinitionService definitionService,
        ILogger<BroadlinkCommandService> logger)
        : base("Broadlink IR Commands", definitionService, logger)
    {
        _deviceLocator = deviceLocator;
        _connectionFactory = connectionFactory;
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

    protected override Command.ExecuteDelegate CreateHandler(IRCommand command)
    {
        byte[] data = Convert.FromBase64String(command.Data);

        return cancellationToken => _connection!.SendDataAsync(data, cancellationToken);
    }
}
