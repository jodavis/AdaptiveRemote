using System.Text;
using AdaptiveRemote.Services;
using Moq;

namespace AdaptiveRemote.TestUtilities;

internal class MockFileSystem : Mock<IFileSystem>
{
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Stream> _files = new(StringComparer.OrdinalIgnoreCase);

    public MockFileSystem()
    {
        Setup(x => x.DirectoryExists(It.IsAny<string>()))
            .Returns(Returns_DirectoryExists);
        Setup(x => x.CreateDirectory(It.IsAny<string>()))
            .Callback(Callback_CreateDirectory);

        Setup(x => x.FileExists(It.IsAny<string>()))
            .Returns(Returns_FileExists);
        Setup(x => x.OpenRead(It.IsAny<string>()))
            .Returns(Returns_OpenRead);
        Setup(x => x.OpenWrite(It.IsAny<string>()))
            .Returns(Returns_OpenWrite);
    }

    public void AddFile(string path)
        => AddFile(path, $"Test File Content for {path}");
    public void AddFile(string path, string content)
    {
        AddDirectory(Path.GetDirectoryName(path));
        _files.Add(path, new MemoryStream(Encoding.UTF8.GetBytes(content)));
    }

    public void AddDirectory(string? path)
    {
        if (path is not null && _directories.Add(path))
        {
            AddDirectory(Path.GetDirectoryName(path));
        }
    }

    public void Expect_CreateDirectory_ForPath(string path)
        => Setup(x => x.CreateDirectory(path))
            .Callback(Callback_CreateDirectory)
            .Verifiable(Times.Once);
    public void Expect_CreateDirectory_IsNotCalled()
        => Setup(x => x.CreateDirectory(It.IsAny<string>()))
            .Verifiable(Times.Never);
    private void Callback_CreateDirectory(string path)
    {
        Assert.IsFalse(_directories.Contains(path), "Attempted to create a directory that already exists: {0}", path);

        string? parent = Path.GetDirectoryName(path);
        Assert.IsNotNull(parent, "Could not compute the parent path for '{0}'", path);
        Assert.IsTrue(_directories.Contains(parent), "Parent path '{0}' does not exist when attempting to create '{1}'", parent, path);

        _directories.Add(path);
    }

    public void Expect_DirectoryExists_ForPath(string path)
        => Setup(x => x.DirectoryExists(path))
            .Returns(Returns_DirectoryExists)
            .Verifiable(Times.Once);
    public void Expect_DirectoryExists_IsNotCalled()
        => Setup(x => x.DirectoryExists(It.IsAny<string>()))
            .Verifiable(Times.Never);
    private bool Returns_DirectoryExists(string path)
        => _directories.Contains(path);

    public void Expect_FileExists_ForPath(string path)
        => Setup(x => x.FileExists(path))
            .Returns(Returns_FileExists)
            .Verifiable(Times.Once);
    public void Expect_FileExists_IsNotCalled()
        => Setup(x => x.FileExists(It.IsAny<string>()))
            .Verifiable(Times.Never);
    private bool Returns_FileExists(string path)
        => _files.ContainsKey(path);

    public void Expect_OpenRead_ForPath(string path)
        => Setup(x => x.OpenRead(path))
            .Returns(Returns_OpenRead)
            .Verifiable(Times.Once);
    public void Expect_OpenRead_IsNotCalled()
        => Setup(x => x.OpenRead(It.IsAny<string>()))
            .Verifiable(Times.Never);
    private Stream Returns_OpenRead(string path)
    {
        Assert.IsTrue(_files.TryGetValue(path, out Stream? fileStream), "Attempted to open file for reading that does not exist: {0}", path);

        fileStream.Seek(0, SeekOrigin.Begin);
        return new DoNotDisposeStream(fileStream, canRead: true);
    }

    public void Expect_OpenWrite_ForPath(string path)
        => Setup(x => x.OpenWrite(path))
            .Returns(Returns_OpenWrite)
            .Verifiable(Times.Once);
    public void Expect_OpenWrite_IsNotCalled()
        => Setup(x => x.OpenWrite(It.IsAny<string>()))
            .Returns(Returns_OpenWrite);
    private Stream Returns_OpenWrite(string path)
    {
        string? parentDirectory = Path.GetDirectoryName(path);
        Assert.IsNotNull(parentDirectory, "Could not compute the parent path for '{0}'", path);
        Assert.IsTrue(_directories.Contains(parentDirectory), "Attempted to open a file for writing in directory that does not exist: {0}", path);

        // OpenWrite defaults to Create behavior, so always create a new stream
        MemoryStream fileStream = new MemoryStream();
        _files[path] = fileStream;

        return new DoNotDisposeStream(fileStream, canWrite: true);
    }

    internal void VerifyFileContents(string path, string expectedContent)
    {
        Assert.IsTrue(_files.TryGetValue(path, out Stream? fileStream), "Expected file was not found: {0}", path);

        long restorePosition = fileStream.Position;
        string actualContent;
        try
        {
            fileStream.Position = 0;
            actualContent = new StreamReader(fileStream).ReadToEnd();
        }
        finally
        {
            fileStream.Position = restorePosition;
        }

        Assert.AreEqual(expectedContent, actualContent, "File content for {0}", path);
    }

    private class DoNotDisposeStream : Stream
    {
        private readonly Stream _inner;

        public DoNotDisposeStream(Stream inner, bool canRead = false, bool canWrite = false, bool canSeek = true)
        {
            _inner = inner;
            CanRead = canRead;
            CanWrite = canWrite;
            CanSeek = canSeek;
        }

        public override bool CanRead { get; }

        public override bool CanSeek { get; }

        public override bool CanWrite { get; }

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    }
}
