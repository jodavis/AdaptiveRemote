namespace AdaptiveRemote.Services;

[TestClass]
public class FileSystemExtensionsTests
{
    private readonly MockFileSystem MockFileSystem = new();

    // Use a cross-platform root path for tests
    private static readonly string Root = OperatingSystem.IsWindows() ? @"C:\" : "/";
    private static readonly string UsersDir = Path.Combine(Root, "users");
    private static readonly string BobDir = Path.Combine(UsersDir, "bob_the_builder");
    private static readonly string TempDir = Path.Combine(BobDir, "temp");
    private static readonly string HatFile = Path.Combine(TempDir, "hat.txt");

    [TestCleanup]
    public void VerifyMocks()
    {
        MockFileSystem.Verify();
    }

    [TestMethod]
    public void FileSystemExtensions_CreateDirectory_Recursive_DoesNothingIfDirectoryExists()
    {
        // Arrange
        MockFileSystem.AddDirectory(TempDir);

        MockFileSystem.Expect_CreateDirectory_IsNotCalled();

        IFileSystem fileSystem = MockFileSystem.Object;

        // Act
        fileSystem.CreateDirectory(TempDir, recursive: true);

        // Assert
    }

    [TestMethod]
    public void FileSystemExtensions_CreateDirectory_Recursive_CreatesOneLevelOfDirectory()
    {
        // Arrange
        MockFileSystem.AddDirectory(BobDir);

        IFileSystem fileSystem = MockFileSystem.Object;

        // Act
        fileSystem.CreateDirectory(TempDir, recursive: true);

        // Assert
        Assert.IsTrue(fileSystem.DirectoryExists(TempDir), "Directory {0} should have been created", TempDir);
    }

    [TestMethod]
    public void FileSystemExtensions_CreateDirectory_Recursive_CreatesMultipleLevelsOfDirectory()
    {
        // Arrange
        MockFileSystem.AddDirectory(Root);

        IFileSystem fileSystem = MockFileSystem.Object;

        // Act
        fileSystem.CreateDirectory(TempDir, recursive: true);

        // Assert
        Assert.IsTrue(fileSystem.DirectoryExists(TempDir), "Directory {0} should have been created", TempDir);
        Assert.IsTrue(fileSystem.DirectoryExists(BobDir), "Directory {0} should have been created", BobDir);
        Assert.IsTrue(fileSystem.DirectoryExists(UsersDir), "Directory {0} should have been created", UsersDir);
    }

    [TestMethod]
    public void FileSystemExtensions_CreateDirectory_Recursive_ThrowsArgumentExceptionForInvalidPath()
    {
        // Arrange
        MockFileSystem.Expect_CreateDirectory_IsNotCalled();
        MockFileSystem.Expect_CreateDirectory_ForPath(Root);

        IFileSystem fileSystem = MockFileSystem.Object;

        try
        {
            // Act
            fileSystem.CreateDirectory(TempDir, recursive: true);

            // Assert
            Assert.Fail("Expected exception was not thrown.");
        }
        catch (AssertFailedException result) when (result.Message.Contains("Could not compute the parent path for"))
        {
            // This is the expected exception
        }
    }

    [TestMethod]
    [Timeout(1000)]
    public void FileSystemExtensions_CreateDirectory_NotRecursive_ThrowsArgumentExceptionForInvalidPath()
    {
        // Arrange
        MockFileSystem.Expect_CreateDirectory_IsNotCalled();
        MockFileSystem.Expect_CreateDirectory_ForPath(TempDir);

        IFileSystem fileSystem = MockFileSystem.Object;

        try
        {
            // Act
            fileSystem.CreateDirectory(TempDir, recursive: false);

            // Assert
            Assert.Fail("Expected exception was not thrown.");
        }
        catch (AssertFailedException result) when (result.Message.Contains("does not exist when attempting to create"))
        {
            // This is the expected exception
        }
    }

    [TestMethod]
    public void FileSystemExtensions_OpenWrite_CreateDirectory_DoesNotCreateDirectoryThatAlreadyExists()
    {
        // Arrange
        MockFileSystem.AddDirectory(TempDir);
        MockFileSystem.Expect_CreateDirectory_IsNotCalled();
        MockFileSystem.Expect_OpenWrite_ForPath(HatFile);

        IFileSystem fileSystem = MockFileSystem.Object;

        // Act
        Stream resultStream = fileSystem.OpenWrite(HatFile, createDirectory: true);

        // Assert
        Assert.IsNotNull(resultStream, nameof(resultStream));
    }

    [TestMethod]
    public void FileSystemExtensions_OpenWrite_CreateDirectory_CreatesDirectoryThatDoesNotExist()
    {
        // Arrange
        MockFileSystem.AddDirectory(UsersDir);
        MockFileSystem.Expect_OpenWrite_ForPath(HatFile);

        IFileSystem fileSystem = MockFileSystem.Object;

        // Act
        Stream resultStream = fileSystem.OpenWrite(HatFile, createDirectory: true);

        // Assert
        Assert.IsNotNull(resultStream, nameof(resultStream));
        Assert.IsTrue(fileSystem.DirectoryExists(BobDir), "bob_the_builder does not exit");
        Assert.IsTrue(fileSystem.DirectoryExists(TempDir), "temp does not exist");
    }

    [TestMethod]
    public void FileSystemExtensions_OpenWrite_DoNotCreateDirectory_ThrowsForDirectoryNotFound()
    {
        // Arrange
        MockFileSystem.AddDirectory(UsersDir);
        MockFileSystem.Expect_OpenWrite_ForPath(HatFile);
        MockFileSystem.Expect_CreateDirectory_IsNotCalled();

        IFileSystem fileSystem = MockFileSystem.Object;

        try
        {
            // Act
            Stream resultStream = fileSystem.OpenWrite(HatFile, createDirectory: false);

            // Assert
            Assert.Fail("Expected exception was not thrown");
        }
        catch (AssertFailedException result) when (result.Message.Contains("Attempted to open a file for writing in directory that does not exist"))
        {
            // This is the expected exception
        }
    }
}
