namespace AdaptiveRemote.Services.Commands;

// TEMPORARY HACK: Something needs to create the ICommandService or else it won't
// initialize the ApplicationCommands
internal class CommandServiceInitializerHack : IScopedLifecycle
{
#pragma warning disable IDE0060 // Remove unused parameter
    public CommandServiceInitializerHack(ICommandService commandService) { }
#pragma warning restore IDE0060 // Remove unused parameter

    string IScopedLifecycle.Name => nameof(CommandServiceInitializerHack);

    Task IScopedLifecycle.CleanUpAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    Task IScopedLifecycle.InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
