using System.Diagnostics;
using System.IO;
using System.Reflection;
using AdaptiveRemote.EndToEndTests.Infrastructure;
using AdaptiveRemote.EndToEndTests.TestServices;

namespace AdaptiveRemote.EndToEndTests;

/// <summary>
/// Base class for end-to-end tests of host applications.
/// Provides common functionality for launching hosts, capturing logs, and controlling via test endpoint.
/// </summary>
public abstract class HostEndToEndTestBase
{
    protected const int DefaultStartupTimeoutSeconds = 120;
    protected const int DefaultShutdownTimeoutSeconds = 30;
    protected const int DefaultRpcTimeoutSeconds = 10;

    public TestContext? TestContext { get; set; }

    /// <summary>
    /// Gets the path to the host executable to test.
    /// </summary>
    protected abstract string GetHostExecutablePath();

    /// <summary>
    /// Gets the expected "ready" message in the logs.
    /// </summary>
    protected abstract string GetReadyLogMessage();

    /// <summary>
    /// Runs a complete E2E test cycle: launch, verify logs, connect, control, shutdown.
    /// </summary>
    protected async Task RunEndToEndTestAsync()
    {
        // Find an available port for the test control endpoint
        int testPort = FindAvailablePort();
        TestContext?.WriteLine($"Using test control port: {testPort}");

        string executablePath = GetHostExecutablePath();
        if (!File.Exists(executablePath))
        {
            Assert.Inconclusive($"Host executable not found: {executablePath}. " +
                "This test requires the host to be built for the current platform.");
        }

        using var hostProcess = new HostProcess(
            executablePath,
            $"--test:ControlPort={testPort}");

        try
        {
            // Start the host process
            TestContext?.WriteLine($"Starting host: {executablePath}");
            hostProcess.Start();
            TestContext?.WriteLine($"Host process started with PID: {hostProcess.ProcessId}");

            // Wait for the host to be ready (with generous timeout)
            var readyMessage = GetReadyLogMessage();
            TestContext?.WriteLine($"Waiting for ready message: '{readyMessage}'");

            bool isReady = await hostProcess.WaitForLogMessageAsync(
                readyMessage,
                TimeSpan.FromSeconds(DefaultStartupTimeoutSeconds));

            if (!isReady)
            {
                TestContext?.WriteLine("=== STDOUT ===");
                TestContext?.WriteLine(hostProcess.Output);
                TestContext?.WriteLine("=== STDERR ===");
                TestContext?.WriteLine(hostProcess.Error);
                Assert.Fail($"Host did not become ready within {DefaultStartupTimeoutSeconds}s. " +
                    $"Expected log message: '{readyMessage}'");
            }

            TestContext?.WriteLine("Host is ready");

            // Connect to the test control endpoint
            using var testClient = new TestControlClient();
            TestContext?.WriteLine($"Connecting to test control endpoint on port {testPort}");

            await testClient.ConnectAsync(
                testPort,
                TimeSpan.FromSeconds(DefaultRpcTimeoutSeconds));

            TestContext?.WriteLine("Connected to test control endpoint");

            // Load the test service into the host
            string testServiceAssemblyPath = GetTestServiceAssemblyPath();
            string testServiceTypeName = typeof(DefaultTestService).FullName!;

            TestContext?.WriteLine($"Loading test service: {testServiceTypeName}");
            await testClient.LoadTestServiceAsync(testServiceAssemblyPath, testServiceTypeName);
            TestContext?.WriteLine("Test service loaded");

            // Verify test service is healthy
            bool isHealthy = await testClient.InvokeAsync<bool>("HealthCheckAsync");
            isHealthy.Should().BeTrue("Test service should be healthy");
            TestContext?.WriteLine("Test service health check passed");

            // Request shutdown via the test service
            TestContext?.WriteLine("Requesting shutdown via test service");
            await testClient.RequestShutdownAsync();

            // Wait for clean shutdown
            TestContext?.WriteLine("Waiting for process to exit");
            bool exited = await hostProcess.WaitForExitAsync(
                TimeSpan.FromSeconds(DefaultShutdownTimeoutSeconds));

            if (!exited)
            {
                TestContext?.WriteLine("Process did not exit cleanly, killing it");
                hostProcess.Kill();
                Assert.Fail($"Host did not shut down within {DefaultShutdownTimeoutSeconds}s");
            }

            TestContext?.WriteLine($"Host exited with code: {hostProcess.ExitCode}");

            // Verify exit code
            hostProcess.ExitCode.Should().Be(0, "Host should exit cleanly with code 0");

            // Verify logs are clean (no unexpected errors or warnings)
            // Note: This is a simplified check. In practice, you'd want more sophisticated log analysis
            // that understands expected vs unexpected errors.
            VerifyLogsAreClean(hostProcess);

            TestContext?.WriteLine("=== Test completed successfully ===");
        }
        catch
        {
            // On failure, dump logs for debugging
            TestContext?.WriteLine("=== Test failed, dumping logs ===");
            TestContext?.WriteLine("=== STDOUT ===");
            TestContext?.WriteLine(hostProcess.Output);
            TestContext?.WriteLine("=== STDERR ===");
            TestContext?.WriteLine(hostProcess.Error);
            throw;
        }
    }

    /// <summary>
    /// Verifies that the captured logs don't contain unexpected errors or warnings.
    /// </summary>
    protected virtual void VerifyLogsAreClean(HostProcess hostProcess)
    {
        // Basic verification - can be enhanced based on actual log patterns
        string fullLog = hostProcess.Output + hostProcess.Error;

        // For now, just verify we got some output
        fullLog.Should().NotBeNullOrEmpty("Should have captured some log output");

        TestContext?.WriteLine($"Log verification passed (captured {fullLog.Length} characters)");
    }

    /// <summary>
    /// Finds an available TCP port for the test control endpoint.
    /// </summary>
    private static int FindAvailablePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(
            System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Gets the path to the test service assembly.
    /// </summary>
    private static string GetTestServiceAssemblyPath()
    {
        // The test service assembly should be in the output directory of the test project
        string testAssemblyLocation = Assembly.GetExecutingAssembly().Location;
        string testDirectory = Path.GetDirectoryName(testAssemblyLocation)!;
        return Path.Combine(testDirectory, "AdaptiveRemote.EndToEndTests.TestServices.dll");
    }
}
