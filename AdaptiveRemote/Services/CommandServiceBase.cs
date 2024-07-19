using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services;

internal abstract class CommandServiceBase<CommandType> : IScopedLifecycle
    where CommandType : Command
{
    private readonly IEnumerable<CommandType> _commands;

    protected CommandServiceBase(string name, IRemoteDefinitionService remoteDefinition)
    {
        Name = name;

        _commands = remoteDefinition.GetCommands<CommandType>().ToList();

        foreach (CommandType command in _commands)
        {
            command.ExecuteAsync = CreateNotStartedHandler(command);
            command.IsEnabled = false;
        }
    }

    public string Name { get; }
    protected abstract ILogger Logger { get; }

    protected abstract Command.ExecuteDelegate CreateHandler(CommandType command);

    public virtual Task InitializeAsync(CancellationToken cancellationToken)
    {
        foreach (CommandType command in _commands)
        {
            command.ExecuteAsync = CreateWrappedHandler(command);
            command.IsEnabled = true;
        }
        return Task.CompletedTask;
    }

    public virtual Task CleanUpAsync(CancellationToken cancellationToken)
    {
        foreach (CommandType command in _commands)
        {
            command.ExecuteAsync = CreateShutDownHandler(command);
            command.IsEnabled = false;
        }
        return Task.CompletedTask;
    }

    private Command.ExecuteDelegate CreateWrappedHandler(CommandType command)
    {
        Command.ExecuteDelegate action = CreateHandler(command);

        return async delegate (CancellationToken cancellationToken)
        {
            try
            {
                command.IsActive = true;

                Logger.LogInformation(Message.CommandService_Executing, command);
                await action(cancellationToken);
                Logger.LogInformation(Message.CommandService_Executed, command);
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning(Message.CommandService_Cancelled, command);
            }
            catch (Exception error)
            {
                Logger.LogError(Message.CommandService_Error, command, error);
            }
            finally
            {
                command.IsActive = false;
            }
        };
    }

    private Command.ExecuteDelegate CreateNotStartedHandler(CommandType command)
    {
        return delegate (CancellationToken cancellationToken)
        {
            Logger.LogError(Message.CommandService_NotStarted, command);
            return Task.FromException(Errors.CommandService_WasShutDown(command));
        };
    }

    private Command.ExecuteDelegate CreateShutDownHandler(CommandType command)
    {
        return delegate (CancellationToken cancellationToken)
        {
            Logger.LogError(Message.CommandService_WasShutDown, command);
            return Task.FromException(Errors.CommandService_NotStarted(command));
        };
    }
}
