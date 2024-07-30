using System.IO;

namespace AdaptiveRemote.Services;

internal interface IFileSystem
{
    void CreateDirectory(string path);
    bool DirectoryExists(string path);
    bool FileExists(string path);
    Stream OpenRead(string path);
    Stream OpenWrite(string path);
}
