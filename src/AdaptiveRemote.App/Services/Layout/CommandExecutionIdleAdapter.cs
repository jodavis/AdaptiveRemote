using System.ComponentModel;
using AdaptiveRemote.Models;
using AdaptiveRemote.Services.CloudAssets;

namespace AdaptiveRemote.Services.Layout;

internal class CommandExecutionIdleAdapter : IScopedLifecycle
{
    private readonly IRemoteDefinitionService _remoteDefinition;
    private readonly IIdleDetector _idleDetector;
    private IReadOnlyList<Command>? _commands;
    private readonly Dictionary<Command, IDisposable> _activeTokens = new();

    public CommandExecutionIdleAdapter(IRemoteDefinitionService remoteDefinition, IIdleDetector idleDetector)
    {
        _remoteDefinition = remoteDefinition;
        _idleDetector = idleDetector;
    }

    public string Name => "Command execution idle adapter";

    public Task InitializeAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        _commands = _remoteDefinition.GetCommands().ToList();
        foreach (Command command in _commands)
        {
            command.PropertyChanged += OnCommandPropertyChanged;
            if (command.IsActive)
            {
                _activeTokens[command] = _idleDetector.StartNonIdle();
            }
        }
        return Task.CompletedTask;
    }

    public Task CleanUpAsync(ILifecycleActivity activity, CancellationToken cancellationToken)
    {
        if (_commands is not null)
        {
            foreach (Command command in _commands)
            {
                command.PropertyChanged -= OnCommandPropertyChanged;
            }
        }
        foreach (IDisposable token in _activeTokens.Values)
        {
            token.Dispose();
        }
        _activeTokens.Clear();
        return Task.CompletedTask;
    }

    private void OnCommandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Command.IsActive))
        {
            return;
        }

        Command command = (Command)sender!;
        if (command.IsActive)
        {
            if (!_activeTokens.ContainsKey(command))
            {
                _activeTokens[command] = _idleDetector.StartNonIdle();
            }
        }
        else
        {
            if (_activeTokens.Remove(command, out IDisposable? token))
            {
                token.Dispose();
            }
        }
    }
}
