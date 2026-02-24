namespace AdaptiveRemote.Services;

internal interface IPersistSettings
{
    void Set(string name, string value);

    /// <summary>
    /// Attempts to retrieve a stored setting value by name.
    /// </summary>
    /// <param name="name">The setting key name.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The stored value, or <see langword="null"/> if the key does not exist.</returns>
    Task<string?> TryGetAsync(string name, CancellationToken cancellationToken);
}
