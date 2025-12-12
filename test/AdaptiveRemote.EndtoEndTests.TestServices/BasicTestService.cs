using AdaptiveRemote.Models;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// Basic implementation of ITestService for E2E testing.
/// Uses RemoteDefinitionService to find and invoke the Exit command by walking the tree.
/// </summary>
public class BasicTestService : ITestService
{
    private readonly RemoteLayoutElement _remoteRoot;

    public BasicTestService(Services.IRemoteDefinitionService remoteDefinitionService)
    {
        _remoteRoot = remoteDefinitionService.RemoteRoot;
    }

    public Task<string> ExecuteTestAsync(string testData)
    {
        return Task.FromResult($"Echo: {testData}");
    }

    public async Task RequestShutdownAsync()
    {
        // Find the Exit command by walking the remote tree
        Command? exitCommand = FindCommandByName(_remoteRoot, "Exit");

        if (exitCommand is null)
        {
            throw new InvalidOperationException("Exit command not found in remote definition service");
        }

        if (exitCommand.ExecuteAsync is null)
        {
            throw new InvalidOperationException("Exit command does not have an ExecuteAsync delegate");
        }

        // Execute the Exit command
        await exitCommand.ExecuteAsync(CancellationToken.None);
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
}
