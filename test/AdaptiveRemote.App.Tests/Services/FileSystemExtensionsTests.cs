namespace AdaptiveRemote.Services;

[TestClass]
public class FileSystemExtensionsTests
{
    private readonly MockFileSystem MockFileSystem = new();

    [TestCleanup]
    public void VerifyMocks()
    {
        MockFileSystem.Verify();
    }

    [TestMethod]
    public void FileSystemExtensions_CreateDirectory_Recursive_DoesNothingIfDirectoryExists()
    {
        // Arrange
        const string input = @"C:\users\bob_the_builder\temp";
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
        const string parent = @"C:\users\bob_the_builder";
        const string input = @"C:\users\bob_the_builder\temp";
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
        const string parent3 = @"C:\";
        const string parent2 = @"C:\users";
        const string parent1 = @"C:\users\bob_the_builder";
        const string input = @"C:\users\bob_the_builder\temp";
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
    public void FileSystemExtensions_CreateDirectory_Recursive_ThrowsArgumentExceptionForInvalidPath()
    {
        // Arrange
        const string input = @"C:\users\bob_the_builder\temp";

        MockFileSystem.Expect_CreateDirectory_IsNotCalled();
        MockFileSystem.Expect_CreateDirectory_ForPath(@"C:\");

        IFileSystem fileSystem = MockFileSystem.Object;

        try
        {
            // Act
            fileSystem.CreateDirectory(input, recursive: true);

            // Assert
            Assert.Fail("Expected exception was not thrown.");
        }
        catch (AssertFailedException result) when (result.Message == @"Assert.IsNotNull failed. Could not compute the parent path for 'C:\'")
        {
            // This is the expected exception
        }
    }

    [TestMethod]
    [Timeout(1000)]
    public void FileSystemExtensions_CreateDirectory_NotRecursive_ThrowsArgumentExceptionForInvalidPath()
    {
        // Arrange
        const string input = @"C:\users\bob_the_builder\temp";

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
        catch (AssertFailedException result) when (result.Message == @"Assert.IsTrue failed. Parent path 'C:\users\bob_the_builder' does not exist when attempting to create 'C:\users\bob_the_builder\temp'")
        {
            // This is the expected exception
        }
    }

    [TestMethod]
    public void FileSystemExtensions_OpenWrite_CreateDirectory_DoesNotCreateDirectoryThatAlreadyExists()
    {
        // Arrange
        const string input = @"C:\users\bob_the_builder\temp\hat.txt";

        MockFileSystem.AddDirectory(@"C:\users\bob_the_builder\temp");
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
        const string input = @"C:\users\bob_the_builder\temp\hat.txt";

        MockFileSystem.AddDirectory(@"C:\users");
        MockFileSystem.Expect_OpenWrite_ForPath(input);

        IFileSystem fileSystem = MockFileSystem.Object;

        // Act
        Stream resultStream = fileSystem.OpenWrite(input, createDirectory: true);

        // Assert
        Assert.IsNotNull(resultStream, nameof(resultStream));
        Assert.IsTrue(fileSystem.DirectoryExists(@"C:\users\bob_the_builder"), "bob_the_builder does not exit");
        Assert.IsTrue(fileSystem.DirectoryExists(@"C:\users\bob_the_builder\temp"), "temp does not exist");
    }

    [TestMethod]
    public void FileSystemExtensions_OpenWrite_DoNotCreateDirectory_ThrowsForDirectoryNotFound()
    {
        // Arrange
        const string input = @"C:\users\bob_the_builder\temp\hat.txt";

        MockFileSystem.AddDirectory(@"C:\users");
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
        catch (AssertFailedException result) when (result.Message == "Assert.IsTrue failed. Attempted to open a file for writing in directory that does not exist: C:\\users\\bob_the_builder\\temp\\hat.txt")
        {
            // This is the expected exception
        }
    }
}
