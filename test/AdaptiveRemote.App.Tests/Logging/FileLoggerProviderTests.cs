using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Logging;

[TestClass]
public class FileLoggerProviderTests
{
    [TestMethod]
    public void FileLoggerProvider_Constructor_WhenTwoProvidersUseSamePath_DoesNotThrowAndWritesEntries()
    {
        // Arrange
        string logFilePath = Path.Combine(Path.GetTempPath(), $"adaptive-remote-{Guid.NewGuid():N}.log");

        try
        {
            using FileLoggerProvider firstProvider = new(logFilePath);
            firstProvider.CreateLogger("first").LogInformation("first");

            // Act
            Action act = () =>
            {
                using FileLoggerProvider secondProvider = new(logFilePath);
                secondProvider.CreateLogger("second").LogInformation("second");
            };

            // Assert
            act.Should().NotThrow();

            string logContent = File.ReadAllText(logFilePath);
            logContent.Should().Contain("first");
            logContent.Should().Contain("second");
        }
        finally
        {
            if (File.Exists(logFilePath))
            {
                File.Delete(logFilePath);
            }
        }
    }
}
