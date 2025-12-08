namespace AdaptiveRemote.Services.Lifecycle;

internal interface IApplicationScopeContainer
{
    /// <summary>
    /// Sets a new application scope. If there was an existing scope, it is released.
    /// </summary>
    /// <param name="scope">The new application scope to set and activate.</param>
    /// <returns>A task that completes when the new scope has been set and any previous scope has been released.</returns>
    internal Task SetScopeAsync(IApplicationScope scope);

    /// <summary>
    /// Releases the specified application scope. Task does not complete until
    /// all callers are done using the application scope
    /// </summary>
    internal Task ReleaseScopeAsync(IApplicationScope scope);
}
