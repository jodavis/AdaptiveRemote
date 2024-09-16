using System.IO;

namespace AdaptiveRemote.Services;

internal static class FileSystemExtensions
{
    public static void CreateDirectory(this IFileSystem fileSystem, string path, bool recursive)
    {
        if (!fileSystem.DirectoryExists(path))
        {
            if (recursive)
            {
                string? parentDiretory = DirectoryFor(path);
                if (parentDiretory is not null)
                {
                    CreateDirectory(fileSystem, parentDiretory, recursive: true);
                }
            }

            fileSystem.CreateDirectory(path);
        }
    }

    public static Stream OpenWrite(this IFileSystem fileSystem, string path, bool createDirectory)
    {
        if (createDirectory)
        {
            CreateDirectory(fileSystem, DirectoryFor(path), recursive: true);
        }

        return fileSystem.OpenWrite(path);
    }

    private static string DirectoryFor(string path) => Path.GetDirectoryName(path)!;
}
