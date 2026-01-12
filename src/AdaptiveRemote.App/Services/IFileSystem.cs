namespace AdaptiveRemote.Services;

/// <summary>
/// Abstraction for the file system
/// </summary>
internal interface IFileSystem
{
    /// <summary>
    /// Creates a directory at the given <paramref name="path"/>. Throws an exception if the
    /// directory cannot be created, e.g. if the parent directory does not exist.
    /// </summary>
    void CreateDirectory(string path);

    /// <summary>
    /// Returns true if a directory exists at the given <paramref name="path"/>
    /// </summary>
    bool DirectoryExists(string path);

    /// <summary>
    /// Returns true if a file exists at the given <paramref name="path"/>
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    bool FileExists(string path);

    /// <summary>
    /// Opens the file at the given <paramref name="path"/> for reading. Throws an exception
    /// if the file cannot be opened, e.g. if it does not exist or is open for writing.
    /// </summary>
    Stream OpenRead(string path);

    /// <summary>
    /// Opens the file at the given <paramref name="path"/> for writing. Throws an exception
    /// if the file cannot be oepend, e.g. if it does not exist or is already open for writing.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    Stream OpenWrite(string path);
}
