# End-to-End Test Architecture

## Overview
The E2E testing subsystem validates that AdaptiveRemote host applications (WPF, Console, and Headless) can start correctly, establish test control connections, execute commands, and shut down cleanly. These tests run as separate processes to validate the complete deployment artifacts.

## Responsibilities & Boundaries
- **Host Process Management:** Launches host binaries via command line, captures output, and manages lifecycle
- **Test Control Channel:** Establishes TCP/JSON-RPC connection for remote test control
- **Service Validation:** Dynamically loads test services to validate DI scope and command execution
- **Log Capture:** Application writes logs to a file for post-mortem analysis

## Key Abstractions

### Host Management
- `AdaptiveRemoteHost` (`test/.../Host/AdaptiveRemoteHost.cs`): Manages the lifecycle of a host application process during testing. Handles process startup, log capture, JSON-RPC connection establishment, and cleanup.
- `AdaptiveRemoteHostSettings` (`test/.../Host/AdaptiveRemoteHostSettings.cs`): Configuration for launching a host, including executable path, command-line arguments, working directory, environment variables, and timeouts.

### Test Orchestration
- `HostTestBase` (`test/.../HostTestBase.cs`): Abstract base class for E2E tests. Provides common functionality for launching hosts, loading test services, and verifying logs.
- `HeadlessHostTests` (`test/.../HeadlessHostTests.cs`), `ConsoleHostTests` (`test/.../ConsoleHostTests.cs`), `WpfHostTests` (`test/.../WpfHostTests.cs`): Concrete test classes for each host variant.

### Test Services
- `IApplicationTestService` (`test/AdaptiveRemote.EndtoEndTests.TestServices/IApplicationTestService.cs`): Interface for the main test service that can be loaded dynamically by the host. Provides methods to wait for lifecycle phases and invoke commands.
- `ApplicationTestService` (`test/AdaptiveRemote.EndtoEndTests.TestServices/ApplicationTestService.cs`): Implementation that uses `IRemoteDefinitionService` to find and invoke commands, demonstrating proper DI scope access.

### Control Endpoint
- `ITestEndpoint` (`src/AdaptiveRemote.App/Services/Testing/ITestEndpoint.cs`): JSON-RPC interface exposed by the host for test control operations. It exposes `CreateTestServiceAsync(...)` and `CreateTestLoggerAsync(...)` to load test-side services and logger targets into the host process.
- `TestEndpointService` (`src/AdaptiveRemote.App/Services/Testing/TestEndpointService.cs`): Background service that listens on TCP port (when `--test:ControlPort` is provided) and handles JSON-RPC requests.

## Architecture Patterns

### Process-Based Testing
Tests launch host applications as separate processes rather than in-process testing. This validates:
- Actual deployment artifacts and binaries
- Complete startup and shutdown sequences
- Process isolation and resource management
- Real-world failure modes

### Dynamic Service Loading
Test services are loaded at runtime without compile-time dependencies from hosts to test assemblies:
1. Test orchestrator connects to host via TCP/JSON-RPC
2. Calls `CreateTestServiceAsync` with assembly path and type name
3. Host loads assembly using `Assembly.LoadFrom`
4. Creates instances within DI scope using `IApplicationScopeProvider`
5. Returns JSON-RPC marshalable proxy for test service

The host also supports `CreateTestLoggerAsync` which loads a test-side logger type into the host scope and returns an `ITestLogger` RPC target that the test process can use as a sink.

### Log Capture and Artifacts
Each test run creates a timestamped log file containing:
- Process startup information (executable, arguments, environment)
- Complete stdout and stderr streams
- Timestamps for debugging timing issues
- Automatic upload to CI artifacts for post-mortem analysis

See `test/AdaptiveRemote.EndtoEndTests.TestServices/Logging/_doc_TestLogging.md` for details.

## Test Flow

```
1. Test Setup
   └─> Create AdaptiveRemoteHostSettings
   └─> Configure executable path, environment variables, timeouts

2. Host Launch
   └─> Start process with test control port
   └─> Begin capturing stdout/stderr to log file
   └─> Wait for TCP connection establishment

3. Test Service Loading
   └─> Connect to test control endpoint via JSON-RPC
   └─> Call CreateTestServiceAsync with test service type
   └─> Receive ITestService proxy
   └─> Optionally call CreateTestLoggerAsync to obtain an ITestLogger RPC target

4. Test Execution
   └─> Wait for application initialization (or skip for headless)
   └─> Invoke test commands via proxy (e.g., Exit command)
   └─> Verify command execution

5. Shutdown and Verification
   └─> Wait for clean process exit
   └─> Verify logs for errors/warnings
   └─> Close log file
   └─> Upload logs as CI artifacts
```

## Platform-Specific Considerations

### Windows Hosts (WPF, Console)
- Executable is a native .exe file
- UI renders normally, creating application scope
- All lifecycle phases complete successfully
- Tests can wait for `Ready` phase

### Linux Host (Electron)
- Executable has no .exe extension
- Requires Xvfb virtual display (`DISPLAY=:99`)
- GPU process fails to initialize (expected in headless)
- Environment variables: `ELECTRON_DISABLE_SANDBOX=1`, `ELECTRON_DISABLE_GPU=1`
- **Challenge:** UI may not render, preventing scope creation
- Tests skip waiting for `Ready` phase in headless mode

## Environment Requirements

### Linux CI
```bash
# Start virtual display
Xvfb :99 -screen 0 1024x768x24 &
export DISPLAY=:99

# Build Electron for Linux
dotnet build src/AdaptiveRemote.Electron/AdaptiveRemote.Electron.csproj -r linux-x64

# Run tests
dotnet test test/AdaptiveRemote.EndtoEndTests
```

### Windows CI
- No special display requirements
- Standard dotnet build and test commands

## Testability

### Timeouts
All blocking operations have configurable timeouts to prevent CI hangs:
- **Startup:** 120 seconds (host must establish TCP connection)
- **RPC calls:** 30 seconds (test service methods)
- **Shutdown:** 30 seconds (process must exit cleanly)

### Synchronous Wrappers
Test code uses synchronous wrappers (`WaitUtilities`) around async RPC calls to simplify test logic and improve debuggability when tests hang.

### Log Files
- Automatically created in test output directory
- Named with timestamp: `{HostName}_{yyyyMMdd_HHmmss}.log`
- Captured by CI as artifacts with 30-day retention
- Include complete stdout/stderr for post-mortem debugging

## Known Issues and Limitations

### Electron in Headless Environments
- Electron UI may fail to render in headless Linux CI environments
- BlazorAppScope creation depends on UI rendering
- Without scope, test services cannot execute (InvokeInScopeAsync blocks)
- **Workaround:** Tests skip waiting for `Ready` phase and use fixed delays
- **Status:** Under investigation; works in some environments but not all

### Scope Dependency
- Test services require application scope to exist
- Scope is created when Blazor UI renders (via `BlazorAppScope`)
- If UI never renders, scope never exists, and tests fail
- This is by design for normal app behavior but complicates headless testing

## Future Enhancements

- **Headless Mode:** Add flag to create scope without UI rendering for testing
- **Log Verification:** Automated parsing of logs to detect errors/warnings
- **Performance Metrics:** Capture startup/shutdown times for regression detection
- **Parallel Execution:** Support running multiple host tests concurrently
- **Custom Test Services:** Framework for test-specific validation scenarios

## Updating This Document
Update this document when:
- Architecture patterns change (e.g., new proxy patterns, different communication protocols)
- New test abstractions are added
- Platform support changes (e.g., new host types, different OS requirements)
- Known issues are resolved or new limitations discovered

For implementation details, refer to source code and inline comments.
