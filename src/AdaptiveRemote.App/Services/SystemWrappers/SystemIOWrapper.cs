namespace AdaptiveRemote.Services.SystemWrappers;

internal class SystemIOWrapper : IFileSystem
{
    public Stream OpenRead(string path) => File.OpenRead(path);

    public Stream OpenWrite(string path) => File.Open(path, FileMode.Create, FileAccess.Write);

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
}
