# End-to-End Tests

This directory contains end-to-end (E2E) tests for the AdaptiveRemote application hosts. These tests verify that each host binary can:

1. Launch successfully
2. Initialize without errors or warnings
3. Respond to test control commands via a TCP JSON-RPC endpoint
4. Shut down cleanly

## Projects

### AdaptiveRemote.EndToEndTests.TestServices
Contains test service abstractions that can be dynamically loaded into the host application during testing. This project has no compile-time dependency on the host applications, ensuring clean separation.

Key components:
- `ITestService`: Interface for test services that can be loaded via JSON-RPC
- `DefaultTestService`: Default implementation providing basic health check and shutdown capabilities

### AdaptiveRemote.EndToEndTests
Contains the MSTest-based E2E tests and infrastructure for launching hosts, capturing logs, and controlling them via the test endpoint.

Key components:
- `HostEndToEndTestBase`: Base class providing common E2E test functionality
- `HostProcess`: Manages host process lifecycle and log capture
- `TestControlClient`: JSON-RPC client for communicating with the test control endpoint
- Test classes: `AdaptiveRemoteEndToEndTests`, `AdaptiveRemoteConsoleEndToEndTests`

## How It Works

### Test Control Endpoint

The host applications can be started with a `--test:ControlPort=<port>` command-line argument, which enables a TCP JSON-RPC test control endpoint. This endpoint allows tests to:

1. Load test service assemblies dynamically at runtime
2. Invoke methods on the loaded test services
3. Request application shutdown

The test control endpoint is implemented by `TestControlService` in `AdaptiveRemote.App`, which is automatically registered when the command-line argument is present.

### Test Flow

1. **Launch**: The test finds an available TCP port and launches the host with `--test:ControlPort=<port>`
2. **Wait for Ready**: The test monitors the host's log output for a "ready" message indicating initialization is complete
3. **Connect**: The test establishes a TCP connection to the test control endpoint
4. **Load Test Service**: The test dynamically loads a test service assembly into the host process via JSON-RPC
5. **Control**: The test invokes methods on the test service (e.g., health check, shutdown request)
6. **Verify Shutdown**: The test waits for the host to exit cleanly and verifies the exit code and logs

### Timeouts

All operations have generous timeouts to prevent CI hangs:
- **Startup**: 120 seconds (2 minutes)
- **Shutdown**: 30 seconds
- **RPC calls**: 10 seconds

If any timeout is exceeded, the test fails and the host process is forcibly killed.

## Running the Tests

### Requirements

These tests require the host applications to be built before running. The tests are marked with `[TestCategory("RequiresWindows")]` because the host applications are Windows-only.

### Running on Windows

```bash
# Build all projects including hosts
dotnet build

# Run E2E tests
dotnet test --filter TestCategory=EndToEnd
```

### Running on Linux/Mac

The E2E tests will be skipped on non-Windows platforms because the host executables cannot be built or run.

## Adding New Tests

To add a new E2E test for a different host:

1. Create a new test class that inherits from `HostEndToEndTestBase`
2. Override `GetHostExecutablePath()` to return the path to the host executable
3. Override `GetReadyLogMessage()` to return the expected "ready" message in logs
4. Add a test method that calls `RunEndToEndTestAsync()`

Example:

```csharp
[TestClass]
public class MyHostEndToEndTests : HostEndToEndTestBase
{
    protected override string GetHostExecutablePath()
    {
        // Return path to your host executable
    }

    protected override string GetReadyLogMessage()
    {
        return "Ready"; // Or your specific ready message
    }

    [TestMethod]
    [TestCategory("EndToEnd")]
    [TestCategory("RequiresWindows")]
    public async Task MyHost_LaunchesAndRespondsToTestControl()
    {
        await RunEndToEndTestAsync();
    }
}
```

## Custom Test Services

You can create custom test services by implementing `ITestService` in the `AdaptiveRemote.EndToEndTests.TestServices` project. The test control endpoint can load and invoke methods on these services at runtime without requiring a compile-time dependency from the host application.

## Troubleshooting

If a test fails, the full log output (stdout and stderr) will be written to the test output for debugging. Look for:

1. Unexpected errors or warnings in the logs
2. Whether the host reached the "ready" state
3. Whether the test control endpoint accepted connections
4. Whether the test service loaded successfully

## Design Principles

1. **No compile-time dependencies**: Host applications don't reference test assemblies
2. **Platform-agnostic infrastructure**: Test infrastructure is cross-platform even though hosts are Windows-only
3. **Minimal host changes**: Only a single command-line argument enables test mode
4. **Generous timeouts**: All operations have reasonable timeouts to prevent hangs
5. **Clean separation**: Test services, test infrastructure, and host code are clearly separated
