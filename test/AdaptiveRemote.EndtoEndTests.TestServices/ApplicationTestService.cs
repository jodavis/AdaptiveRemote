using AdaptiveRemote.Models;
using AdaptiveRemote.Services.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdaptiveRemote.EndtoEndTests;

/// <summary>
/// Basic implementation of ITestService for E2E testing.
/// Uses IRemoteDefinitionService to find and invoke the Exit command, demonstrating
/// that the test service has access to the properly scoped services including commands.
/// </summary>
public class ApplicationTestService : IApplicationTestService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LifecycleView _lifecycleView;
    private readonly IHostApplicationLifetime _applicationLifetime;

    // Resolved lazily so that GetCurrentPhaseAsync works in FatalError state,
    // where the layout asset is absent and IRemoteDefinitionService cannot be built.
    private Services.IRemoteDefinitionService RemoteDefinitionService
        => _serviceProvider.GetRequiredService<Services.IRemoteDefinitionService>();

    public ApplicationTestService(
        IServiceProvider serviceProvider,
        LifecycleView lifecycleView,
        IHostApplicationLifetime applicationLifetime)
    {
        _serviceProvider = serviceProvider;
        _lifecycleView = lifecycleView;
        _applicationLifetime = applicationLifetime;
    }

    public async Task InvokeCommandAsync(string commandName, CancellationToken cancellationToken)
    {
        // Find the Exit command by walking the remote tree
        Command command = FindCommandByName(RemoteDefinitionService.RemoteRoot, commandName)
            ?? throw new InvalidOperationException($"{commandName} command not found in remote definition service");

        if (command.ExecuteAsync is null)
        {
            throw new InvalidOperationException($"{commandName} command does not have an ExecuteAsync delegate");
        }

        // Execute the Exit command
        await command.ExecuteAsync(CancellationToken.None);
    }

    public async Task StopApplicationAsync(CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested)
        {
            _applicationLifetime.StopApplication();
        }
    }

    public Task<bool> GetIsListeningAsync(CancellationToken cancellationToken)
    {
        // Find the ConversationView by walking the remote tree
        ConversationView? conversationView = FindConversationView(RemoteDefinitionService.RemoteRoot)
            ?? throw new InvalidOperationException("ConversationView not found in remote definition service");

        // Use GetValue to access the internal property
        bool isListening = conversationView.GetValue(ConversationView.IsListeningProperty);
        return Task.FromResult(isListening);
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

    private static ConversationView? FindConversationView(RemoteLayoutElement element)
    {
        if (element is ConversationView conversationView)
        {
            return conversationView;
        }

        if (element is LayoutGroup group)
        {
            foreach (RemoteLayoutElement child in group.Elements)
            {
                ConversationView? found = FindConversationView(child);
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
