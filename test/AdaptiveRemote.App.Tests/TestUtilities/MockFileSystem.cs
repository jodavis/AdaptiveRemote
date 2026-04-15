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
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            AddDirectory(directory);
        }
        _files.Add(path, new MemoryStream(Encoding.UTF8.GetBytes(content)));
    }

    public void AddDirectory(string? path)
    {
        if (!string.IsNullOrEmpty(path) && _directories.Add(path))
        {
            string? parentDirectory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parentDirectory))
            {
                AddDirectory(parentDirectory);
            }
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
        if (!string.IsNullOrEmpty(parent))
        {
            Assert.IsTrue(_directories.Contains(parent), "Parent path '{0}' does not exist when attempting to create '{1}'", parent, path);
        }

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
    public void Expect_OpenRead_IsNotCalledForPath(string path)
        => Setup(x => x.OpenRead(path))
            .Throws(new AssertFailedException($"OpenRead should not be called with path: {path}"))
            .Verifiable(Times.Never);
    public void Expect_OpenRead_IsNotCalled()
        => Setup(x => x.OpenRead(It.IsAny<string>()))
            .Verifiable(Times.Never);
    private Stream Returns_OpenRead(string path)
    {
        Stream fileStream = GetReadableFileStreamFor(path);

        fileStream.Seek(0, SeekOrigin.Begin);
        return new DoNotDisposeStream(fileStream, canRead: true);
    }

    public void Expect_OpenWrite_ForPath(string path, Times? times = default)
        => Setup(x => x.OpenWrite(path))
            .Returns(Returns_OpenWrite)
            .Verifiable(times ?? Times.Once());
    public void Expect_OpenWrite_IsNotCalledForPath(string path)
        => Setup(x => x.OpenWrite(path))
            .Verifiable(Times.Never);
    public void Expect_OpenWrite_IsNotCalled()
        => Setup(x => x.OpenWrite(It.IsAny<string>()))
            .Returns(Returns_OpenWrite)
            .Verifiable(Times.Never);
    private Stream Returns_OpenWrite(string path)
    {
        string? parentDirectory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parentDirectory))
        {
            Assert.IsTrue(_directories.Contains(parentDirectory), "Attempted to open a file for writing in directory that does not exist: {0}", path);
        }

        if (_files.TryGetValue(path, out Stream? existingStream) &&
            existingStream is DoNotDisposeStream writeStream)
        {
            Assert.IsFalse(writeStream.IsOpenForWrite, "Attempted to open file for reading while it is open for writing: {0}", path);
        }

        // OpenWrite defaults to Create behavior, so always create a new stream
        DoNotDisposeStream fileStream = new(new MemoryStream(), canWrite: true);
        _files[path] = fileStream;

        return fileStream;
    }

    internal void VerifyFileContents(string path, string expectedContent)
    {
        Assert.AreEqual(expectedContent, GetFileContents(path), "File content for {0}", path);
    }

    internal string GetFileContents(string path)
    {
        Stream fileStream = GetReadableFileStreamFor(path);

        long restorePosition = fileStream.Position;
        try
        {
            fileStream.Position = 0;
            return new StreamReader(fileStream).ReadToEnd();
        }
        finally
        {
            fileStream.Position = restorePosition;
        }
    }

    private Stream GetReadableFileStreamFor(string path)
    {
        Assert.IsTrue(_files.TryGetValue(path, out Stream? fileStream), "Attempted to open file for reading that does not exist: {0}", path);
        if (fileStream is DoNotDisposeStream writeStream)
        {
            Assert.IsFalse(writeStream.IsOpenForWrite, "Attempted to open file for reading while it is open for writing: {0}", path);
            fileStream = writeStream.Inner;
        }

        return fileStream;
    }

    private class DoNotDisposeStream : Stream
    {
        public DoNotDisposeStream(Stream inner, bool canRead = false, bool canWrite = false, bool canSeek = true)
        {
            Inner = inner;
            CanRead = canRead;
            CanWrite = canWrite;
            CanSeek = canSeek;
        }

        public Stream Inner { get; }
        public bool IsOpenForWrite { get; private set; } = true;

        public override bool CanRead { get; }
        public override bool CanSeek { get; }
        public override bool CanWrite { get; }
        public override long Length => Inner.Length;
        public override long Position
        {
            get => Inner.Position;
            set => Inner.Position = value;
        }

        protected override void Dispose(bool disposing)
        {
            IsOpenForWrite = false;
            base.Dispose(disposing);
        }

        public override void Flush() => Inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => Inner.Seek(offset, origin);
        public override void SetLength(long value) => Inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => Inner.Write(buffer, offset, count);
    }
}
