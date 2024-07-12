using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services.Broadlink;

internal class BroadlinkCommandService(
    IDeviceLocator _deviceLocator,
    IDeviceConnectionFactory _connectionFactory,
    IRemoteDefinitionService _definitionService,
    ILogger<BroadlinkCommandService> _logger)
    : IScopedLifecycle
{
    string IScopedLifecycle.Name => "Broadlink IR System";

    async Task IScopedLifecycle.InitializeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(Message.BroadlinkCommandService_SearchingForDevice);
        ScanResponsePacket found = await _deviceLocator.FindDeviceAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        IDeviceConnection connection = _connectionFactory.Create(found.HostEndPoint, found.HostAddress, found.DeviceType);

        _logger.LogInformation(Message.BroadlinkCommandService_Authenticating, found.HostEndPoint);
        await connection.AuthenticateAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(Message.BroadlinkCommandService_Authenticated, found.HostEndPoint);

        foreach (IRCommand command in _definitionService.GetCommands().OfType<IRCommand>())
        {
            byte[] data = Convert.FromBase64String(command.Data);

            command.AddActionWithLogging(_logger, CreateActionDelegate(connection, data));
        }

        _logger.LogInformation(Message.BroadlinkCommandService_Ready);

        static Command.ExecuteDelegate CreateActionDelegate(IDeviceConnection connection, byte[] data)
            => cancellationToken => connection.SendData(data, cancellationToken);
    }

    Task IScopedLifecycle.CleanUpAsync(CancellationToken cancellationToken)
    {
        foreach (IRCommand command in _definitionService.GetCommands().OfType<IRCommand>())
        {
            command.ExecuteAsync = null;
            command.IsEnabled = false;
        }

        return Task.CompletedTask;
    }
}

internal static class CommandExtensions
{
    internal static void AddActionWithLogging(this Command command, ILogger logger, Command.ExecuteDelegate action)
    {
        command.ExecuteAsync = async cancellationToken =>
        {
            try
            {
                command.IsActive = true;

                logger.LogInformation(Message.CommandService_Executing, command);
                await action(cancellationToken);
                logger.LogInformation(Message.CommandService_Executed, command);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning(Message.CommandService_Cancelled, command);
            }
            catch (Exception error)
            {
                logger.LogError(Message.CommandService_Error, command, error);
            }
            finally
            {
                command.IsActive = false;
            }
        };
        command.IsEnabled = true;
    }

    internal static void ClearAction(this Command command)
    {
        command.ExecuteAsync = null;
        command.IsEnabled = false;
    }
}
