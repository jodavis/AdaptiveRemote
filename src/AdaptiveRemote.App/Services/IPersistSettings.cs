namespace AdaptiveRemote.Services;

internal interface IPersistSettings
{
    void Set(string name, string value);

    /// <summary>
    /// Gets the value associated with the specified name, or <see langword="null"/> if not found.
    /// </summary>
    Task<string?> GetAsync(string name);
}
