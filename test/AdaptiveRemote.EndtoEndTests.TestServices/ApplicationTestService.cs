using AdaptiveRemote.Models;
using AdaptiveRemote.Services.Testing;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// Basic implementation of ITestService for E2E testing.
/// Uses IRemoteDefinitionService to find and invoke the Exit command, demonstrating
/// that the test service has access to the properly scoped services including commands.
/// </summary>
public class ApplicationTestService : IApplicationTestService
{
    private readonly Services.IRemoteDefinitionService _remoteDefinitionService;
    private readonly LifecycleView _lifecycleView;

    public ApplicationTestService(Services.IRemoteDefinitionService remoteDefinitionService, LifecycleView lifecycleView)
    {
        _remoteDefinitionService = remoteDefinitionService;
        _lifecycleView = lifecycleView;
    }

    public async Task InvokeCommandAsync(string commandName, CancellationToken cancellationToken)
    {
        // Find the Exit command by walking the remote tree
        Command command = FindCommandByName(_remoteDefinitionService.RemoteRoot, commandName)
            ?? throw new InvalidOperationException($"{commandName} command not found in remote definition service");

        if (command.ExecuteAsync is null)
        {
            throw new InvalidOperationException($"{commandName} command does not have an ExecuteAsync delegate");
        }

        // Execute the Exit command
        await command.ExecuteAsync(CancellationToken.None);
    }

    private static Command? FindCommandByName(RemoteLayoutElement element, string name)
    {
        if (element is Command command && command.Name == name)
        {
            return command;
        }

        if (element is LayoutGroup group)
        {
            foreach (RemoteLayoutElement child in group.Elements)
            {
                Command? found = FindCommandByName(child, name);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    public void Dispose()
    {
        // No resources to dispose, but the proxy requires IDisposable
        GC.SuppressFinalize(this);
    }

    public Task<LifecyclePhase> GetCurrentPhaseAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_lifecycleView.CurrentPhase);
    }
}
