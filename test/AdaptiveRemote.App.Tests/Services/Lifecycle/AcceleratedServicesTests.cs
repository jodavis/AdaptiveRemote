using AdaptiveRemote;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace AdaptiveRemote.Services.Lifecycle;

[TestClass]
public class AcceleratedServicesTests
{
    [TestMethod]
    public void AcceleratedServices_Constructor_InitializesArgs()
    {
        // Arrange
        string[] args = ["--foo=bar"];

        // Act
        AcceleratedServices sut = new(args);

        // Assert
        sut.Args.Should().BeSameAs(args);
    }

    [TestMethod]
    public void AcceleratedServices_Constructor_BuildsStartupConfigFromCommandLineArgs()
    {
        // Arrange
        string[] args = ["--custom=value"];

        // Act
        AcceleratedServices sut = new(args);

        // Assert
        sut.StartupConfig["custom"].Should().Be("value");
    }

    [TestMethod]
    public void AcceleratedServices_StartupConfig_CommandLineArgsOverrideEnvironmentVariables()
    {
        // Arrange
        string envVarName = "ACCELERATED_SERVICES_TEST_VAR";
        Environment.SetEnvironmentVariable(envVarName, "env-value");
        try
        {
            string[] args = [$"--{envVarName}=cli-value"];

            // Act
            AcceleratedServices sut = new(args);

            // Assert
            sut.StartupConfig[envVarName].Should().Be("cli-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [TestMethod]
    public void AcceleratedServices_StartupConfig_IncludesEnvironmentVariables()
    {
        // Arrange
        string envVarName = "ACCELERATED_SERVICES_TEST_ENV";
        Environment.SetEnvironmentVariable(envVarName, "env-value");
        try
        {
            string[] args = [];

            // Act
            AcceleratedServices sut = new(args);

            // Assert
            sut.StartupConfig[envVarName].Should().Be("env-value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [TestMethod]
    public void AcceleratedServices_LoggerFactory_IsConfigured()
    {
        // Arrange
        string[] args = [];

        // Act
        AcceleratedServices sut = new(args);

        // Assert
        sut.LoggerFactory.Should().NotBeNull();
    }

    [TestMethod]
    public void AcceleratedServices_ViewModel_IsInitialized()
    {
        // Arrange
        string[] args = [];

        // Act
        AcceleratedServices sut = new(args);

        // Assert
        sut.ViewModel.Should().NotBeNull();
        sut.ViewModel.CurrentPhase.Should().Be(LifecyclePhase.Waiting);
    }

    [TestMethod]
    public void AcceleratedServices_Controller_IsInitialized()
    {
        // Arrange
        string[] args = [];

        // Act
        AcceleratedServices sut = new(args);

        // Assert
        sut.Controller.Should().NotBeNull();
    }

    [TestMethod]
    public void AcceleratedServices_DiagnosticAdapter_IsInitialized()
    {
        // Arrange
        string[] args = [];

        // Act
        AcceleratedServices sut = new(args);

        // Assert
        sut.DiagnosticAdapter.Should().NotBeNull();
    }

    [TestMethod]
    public void AcceleratedServices_TestEndpoint_StartsWhenControlPortConfigured()
    {
        // Arrange
        int port = Random.Shared.Next(10000, 60000);
        string[] args = [$"--test:ControlPort={port}"];

        // Act
        AcceleratedServices sut = new(args);

        // Assert
        sut.TestEndpoint.Should().NotBeNull();
        sut.TestEndpoint.Should().BeOfType<Testing.TestEndpointService>();
    }

    [TestMethod]
    public void AcceleratedServices_TestEndpoint_DisabledWhenControlPortNotConfigured()
    {
        // Arrange
        string[] args = [];

        // Act
        AcceleratedServices sut = new(args);

        // Assert
        sut.TestEndpoint.Should().NotBeNull();
        sut.TestEndpoint.Should().BeOfType<Testing.DisabledTestEndpointHooks>();
    }
}
