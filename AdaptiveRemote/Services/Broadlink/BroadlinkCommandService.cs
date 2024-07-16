using AdaptiveRemote.Logging;
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
        ILogger<BroadlinkCommandService> _logger)
        : base("Broadlink IR System", definitionService)
    {
        _deviceLocator = deviceLocator;
        _connectionFactory = connectionFactory;
        Logger = _logger;
    }

    protected override ILogger Logger { get; }

    public async override Task InitializeAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation(Message.BroadlinkCommandService_SearchingForDevice);
        ScanResponsePacket found = await _deviceLocator.FindDeviceAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        _connection = _connectionFactory.Create(found.HostEndPoint, found.HostAddress, found.DeviceType);

        Logger.LogInformation(Message.BroadlinkCommandService_Authenticating, found.HostEndPoint);
        await _connection.AuthenticateAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        Logger.LogInformation(Message.BroadlinkCommandService_Authenticated, found.HostEndPoint);

        await base.InitializeAsync(cancellationToken);

        Logger.LogInformation(Message.BroadlinkCommandService_Ready);
    }

    protected override Command.ExecuteDelegate CreateHandler(IRCommand command)
    {
        byte[] data = Convert.FromBase64String(command.Data);

        return cancellationToken => _connection!.SendData(data, cancellationToken);
    }
}
