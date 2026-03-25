using FluentAssertions;

namespace AdaptiveRemote.Services.Lifecycle;

[TestClass]
public class AcceleratedServicesTests
{
    [TestMethod]
    public void AcceleratedServices_StartupConfig_ReadsCommandLineArgs()
    {
        // Arrange / Act
        AcceleratedServices sut = new(["--SomeKey", "TestValue123"]);

        // Assert
        sut.StartupConfig["SomeKey"].Should().Be("TestValue123");
    }

    [TestMethod]
    public void AcceleratedServices_StartupConfig_ReadsEnvironmentVariables()
    {
        // Arrange
        string envKey = "ADAPTIVEREMOTE_TEST_" + Guid.NewGuid().ToString("N");
        System.Environment.SetEnvironmentVariable(envKey, "EnvValue");

        try
        {
            // Act
            AcceleratedServices sut = new([]);

            // Assert
            sut.StartupConfig[envKey].Should().Be("EnvValue");
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(envKey, null);
        }
    }

    [TestMethod]
    public void AcceleratedServices_StartupConfig_CommandLineArgsOverrideEnvironmentVariables()
    {
        // Arrange
        string envKey = "ADAPTIVEREMOTE_TEST_" + Guid.NewGuid().ToString("N");
        System.Environment.SetEnvironmentVariable(envKey, "EnvValue");

        try
        {
            // Act
            AcceleratedServices sut = new([$"--{envKey}", "CliValue"]);

            // Assert
            sut.StartupConfig[envKey].Should().Be("CliValue");
        }
        finally
        {
            System.Environment.SetEnvironmentVariable(envKey, null);
        }
    }
}
