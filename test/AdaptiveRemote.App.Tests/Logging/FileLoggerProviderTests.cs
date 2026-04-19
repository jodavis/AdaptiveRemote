using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace AdaptiveRemote.Logging;

[TestClass]
public class FileLoggerProviderTests
{
    [TestMethod]
    public void FileLoggerProvider_Constructor_AllowsConcurrentWritersForSamePath()
    {
        // Arrange
        string logFilePath = Path.Combine(Path.GetTempPath(), $"adaptive-remote-{Guid.NewGuid():N}.log");

        try
        {
            using FileLoggerProvider firstProvider = new(logFilePath);

            // Act
            Action act = () =>
            {
                using FileLoggerProvider secondProvider = new(logFilePath);
                firstProvider.CreateLogger("first").LogInformation("first");
                secondProvider.CreateLogger("second").LogInformation("second");
            };

            // Assert
            act.Should().NotThrow();
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
