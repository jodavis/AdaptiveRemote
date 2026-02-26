using AdaptiveRemote.Logging;
using AdaptiveRemote.Models;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Services;

internal abstract class CommandServiceBase<CommandType> : IScopedLifecycle
    where CommandType : Command
{
    private readonly IEnumerable<CommandType> _commands;
    private readonly CancellationTokenSource _stop = new();
    private readonly HashSet<Command> _programmableCommands = [];

    protected CommandServiceBase(string name, IRemoteDefinitionService remoteDefinition, ILogger logger)
    {
        Name = name;
        Logger = new(logger);

        _commands = remoteDefinition.GetCommands<CommandType>().ToList();

        foreach (Command command in _commands)
        {
            command.ExecuteAsync = CreateNotStartedHandler(command);
            command.IsEnabled = false;
            command.IsActive = false;
        }
    }

    public string Name { get; }
    protected MessageLogger Logger { get; }

    protected abstract Command.ExecuteDelegate CreateHandler(CommandType command);

    protected virtual Command.ExecuteDelegate? CreateProgramHandler(CommandType command) => null;

    protected virtual bool IsCommandEnabled(CommandType command) => true;

    public virtual Task InitializeAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        foreach (CommandType command in _commands)
        {
            command.ExecuteAsync = CreateWrappedHandler(command, CreateHandler(command));
            command.IsEnabled = IsCommandEnabled(command);

            Command.ExecuteDelegate? programHandler = CreateProgramHandler(command);
            if (programHandler != null)
            {
                command.ProgramAsync = CreateWrappedProgramHandler(command, programHandler);
                _programmableCommands.Add(command);
            }
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

            if (_programmableCommands.Contains(command))
            {
                command.ProgramAsync = CreateWasShutDownProgramHandler(command);
            }
        }
    }

    private Command.ExecuteDelegate CreateWrappedHandler(CommandType command, Command.ExecuteDelegate callback)
    {
        return async delegate (CancellationToken cancellationToken)
        {
            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);

            try
            {
                command.IsActive = true;

                Logger.CommandService_Executing(command);
                await callback(linked.Token);
                Logger.CommandService_Executed(command);
            }
            catch (OperationCanceledException)
            {
                Logger.CommandService_Cancelled(command);
                throw;
            }
            catch (Exception error)
            {
                Logger.CommandService_Error(command, error);
                throw;
            }
            finally
            {
                command.IsActive = false;
            }
        };
    }

    private Command.ExecuteDelegate CreateWrappedProgramHandler(CommandType command, Command.ExecuteDelegate callback)
    {
        return async delegate (CancellationToken cancellationToken)
        {
            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);

            try
            {
                command.IsActive = true;

                Logger.CommandService_Programming(command);
                await callback(linked.Token);
                Logger.CommandService_Programmed(command);
            }
            catch (OperationCanceledException)
            {
                Logger.CommandService_ProgramCancelled(command);
                throw;
            }
            catch (Exception error)
            {
                Logger.CommandService_ProgramError(command, error);
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
            Logger.CommandService_NotStarted(command);
            return Task.FromException(Errors.CommandService_NotStarted(command));
        };
    }

    private Command.ExecuteDelegate CreateWasShutDownHandler(Command command)
    {
        return delegate (CancellationToken _)
        {
            Logger.CommandService_WasShutDown(command);
            return Task.FromException(Errors.CommandService_WasShutDown(command));
        };
    }

    private Command.ExecuteDelegate CreateWasShutDownProgramHandler(Command command)
    {
        return delegate (CancellationToken _)
        {
            Logger.CommandService_ProgramWasShutDown(command);
            return Task.FromException(Errors.CommandService_ProgramWasShutDown(command));
        };
    }
}
