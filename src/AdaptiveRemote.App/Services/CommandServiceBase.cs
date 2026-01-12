using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services;

internal abstract class CommandServiceBase<CommandType> : IScopedLifecycle
    where CommandType : Command
{
    private readonly IEnumerable<CommandType> _commands;
    private readonly CancellationTokenSource _stop = new();

    protected CommandServiceBase(string name, IRemoteDefinitionService remoteDefinition, ILogger logger)
    {
        Name = name;
        Logger = logger;

        _commands = remoteDefinition.GetCommands<CommandType>().ToList();

        foreach (Command command in _commands)
        {
            command.ExecuteAsync = CreateNotStartedHandler(command);
            command.IsEnabled = false;
            command.IsActive = false;
        }
    }

    public string Name { get; }
    protected ILogger Logger { get; }

    protected abstract Command.ExecuteDelegate CreateHandler(CommandType command);

    public virtual Task InitializeAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        foreach (CommandType command in _commands)
        {
            command.ExecuteAsync = CreateWrappedHandler(command);
            command.IsEnabled = true;
        }
        return Task.CompletedTask;
    }

    public virtual async Task CleanUpAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        await _stop.CancelAsync();

        foreach (Command command in _commands)
        {
            command.IsEnabled = false;
            command.ExecuteAsync = CreateWasShutDownHandler(command);
        }
    }

    private Command.ExecuteDelegate CreateWrappedHandler(CommandType command)
    {
        Command.ExecuteDelegate callback = CreateHandler(command);

        return async delegate (CancellationToken cancellationToken)
        {
            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);

            try
            {
                command.IsActive = true;

                Logger.LogInformation(Message.CommandService_Executing, command);
                await callback(linked.Token);
                Logger.LogInformation(Message.CommandService_Executed, command);
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning(Message.CommandService_Cancelled, command);
                throw;
            }
            catch (Exception error)
            {
                Logger.LogError(Message.CommandService_Error, command, error);
                throw;
            }
            finally
            {
                command.IsActive = false;
            }
        };
    }

    private Command.ExecuteDelegate CreateNotStartedHandler(Command command)
    {
        return delegate (CancellationToken _)
        {
            Logger.LogError(Message.CommandService_NotStarted, command);
            return Task.FromException(Errors.CommandService_NotStarted(command));
        };
    }

    private Command.ExecuteDelegate CreateWasShutDownHandler(Command command)
    {
        return delegate (CancellationToken _)
        {
            Logger.LogError(Message.CommandService_WasShutDown, command);
            return Task.FromException(Errors.CommandService_WasShutDown(command));
        };
    }
}
