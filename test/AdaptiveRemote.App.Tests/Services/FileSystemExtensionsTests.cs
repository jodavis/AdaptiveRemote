namespace AdaptiveRemote.Services;

[TestClass]
public class FileSystemExtensionsTests
{
    private readonly MockFileSystem MockFileSystem = new();

    // Platform-agnostic path helpers
    private static string GetTestRoot() => Path.Combine("users", "bob_the_builder");
    private static string GetTestPath() => Path.Combine("users", "bob_the_builder", "temp");
    private static string GetTestFilePath() => Path.Combine("users", "bob_the_builder", "temp", "hat.txt");

    [TestCleanup]
    public void VerifyMocks()
    {
        MockFileSystem.Verify();
    }

    [TestMethod]
    public void FileSystemExtensions_CreateDirectory_Recursive_DoesNothingIfDirectoryExists()
    {
        // Arrange
        string input = GetTestPath();
        MockFileSystem.AddDirectory(input);

        MockFileSystem.Expect_CreateDirectory_IsNotCalled();

        IFileSystem fileSystem = MockFileSystem.Object;

        // Act
        fileSystem.CreateDirectory(input, recursive: true);

        // Assert
    }

    [TestMethod]
    public void FileSystemExtensions_CreateDirectory_Recursive_CreatesOneLevelOfDirectory()
    {
        // Arrange
        string parent = GetTestRoot();
        string input = GetTestPath();
        MockFileSystem.AddDirectory(parent);

        IFileSystem fileSystem = MockFileSystem.Object;

        // Act
        fileSystem.CreateDirectory(input, recursive: true);

        // Assert
        Assert.IsTrue(fileSystem.DirectoryExists(input), "Directory {0} should have been created", input);
    }

    [TestMethod]
    public void FileSystemExtensions_CreateDirectory_Recursive_CreatesMultipleLevelsOfDirectory()
    {
        // Arrange
        string parent3 = "users";
        string parent2 = GetTestRoot();
        string parent1 = GetTestPath();
        string input = Path.Combine("users", "bob_the_builder", "temp", "deep");
        MockFileSystem.AddDirectory(parent3);

        IFileSystem fileSystem = MockFileSystem.Object;

        // Act
        fileSystem.CreateDirectory(input, recursive: true);

        // Assert
        Assert.IsTrue(fileSystem.DirectoryExists(input), "Directory {0} should have been created", input);
        Assert.IsTrue(fileSystem.DirectoryExists(parent1), "Directory {0} should have been created", parent1);
        Assert.IsTrue(fileSystem.DirectoryExists(parent2), "Directory {0} should have been created", parent2);
    }

    [TestMethod]
    public void FileSystemExtensions_CreateDirectory_Recursive_CreatesRootLevelDirectory()
    {
        // Arrange - This test verifies that root-level directories can be created
        // (which replaces the old test that expected this to fail on Windows)
        string rootDir = "users";
        
        IFileSystem fileSystem = MockFileSystem.Object;

        // Act
        fileSystem.CreateDirectory(rootDir, recursive: true);

        // Assert
        Assert.IsTrue(fileSystem.DirectoryExists(rootDir), "Root directory {0} should have been created", rootDir);
    }

    [TestMethod]
    [Timeout(1000)]
    public void FileSystemExtensions_CreateDirectory_NotRecursive_ThrowsArgumentExceptionForInvalidPath()
    {
        // Arrange
        string input = GetTestPath();

        MockFileSystem.Expect_CreateDirectory_IsNotCalled();
        MockFileSystem.Expect_CreateDirectory_ForPath(input);

        IFileSystem fileSystem = MockFileSystem.Object;

        try
        {
            // Act
            fileSystem.CreateDirectory(input, recursive: false);

            // Assert
            Assert.Fail("Expected exception was not thrown.");
        }
        catch (AssertFailedException result) when (result.Message.Contains("Parent path") && result.Message.Contains("does not exist when attempting to create"))
        {
            // This is the expected exception - parent directory doesn't exist
        }
    }

    [TestMethod]
    public void FileSystemExtensions_OpenWrite_CreateDirectory_DoesNotCreateDirectoryThatAlreadyExists()
    {
        // Arrange
        string input = GetTestFilePath();
        string directory = Path.GetDirectoryName(input)!;

        MockFileSystem.AddDirectory(directory);
        MockFileSystem.Expect_CreateDirectory_IsNotCalled();
        MockFileSystem.Expect_OpenWrite_ForPath(input);

        IFileSystem fileSystem = MockFileSystem.Object;

        // Act
        Stream resultStream = fileSystem.OpenWrite(input, createDirectory: true);

        // Assert
        Assert.IsNotNull(resultStream, nameof(resultStream));
    }

    [TestMethod]
    public void FileSystemExtensions_OpenWrite_CreateDirectory_CreatesDirectoryThatDoesNotExist()
    {
        // Arrange
        string input = GetTestFilePath();
        string rootDir = "users";

        MockFileSystem.AddDirectory(rootDir);
        MockFileSystem.Expect_OpenWrite_ForPath(input);

        IFileSystem fileSystem = MockFileSystem.Object;

        // Act
        Stream resultStream = fileSystem.OpenWrite(input, createDirectory: true);

        // Assert
        Assert.IsNotNull(resultStream, nameof(resultStream));
        Assert.IsTrue(fileSystem.DirectoryExists(GetTestRoot()), "bob_the_builder does not exist");
        Assert.IsTrue(fileSystem.DirectoryExists(GetTestPath()), "temp does not exist");
    }

    [TestMethod]
    public void FileSystemExtensions_OpenWrite_DoNotCreateDirectory_ThrowsForDirectoryNotFound()
    {
        // Arrange
        string input = GetTestFilePath();
        string rootDir = "users";

        MockFileSystem.AddDirectory(rootDir);
        MockFileSystem.Expect_OpenWrite_ForPath(input);
        MockFileSystem.Expect_CreateDirectory_IsNotCalled();

        IFileSystem fileSystem = MockFileSystem.Object;

        try
        {
            // Act
            Stream resultStream = fileSystem.OpenWrite(input, createDirectory: false);

            // Assert
            Assert.Fail("Expected exception was not thrown");
        }
        catch (AssertFailedException result) when (result.Message.Contains("Attempted to open a file for writing in directory that does not exist"))
        {
            // This is the expected exception
        }
    }
}
