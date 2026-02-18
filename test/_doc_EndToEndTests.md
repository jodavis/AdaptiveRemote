# End-to-End Test Architecture

## Overview
The E2E testing subsystem validates that AdaptiveRemote host applications (WPF, Console, and Headless) can start correctly, establish test control connections, execute commands, and shut down cleanly. These tests run as separate processes to validate the complete deployment artifacts.

## Responsibilities & Boundaries
- **Host Process Management:** Launches host binaries via command line, captures output, and manages lifecycle
- **Test Control Channel:** Establishes TCP/JSON-RPC connection for remote test control
- **Service Validation:** Dynamically loads test services to validate DI scope and command execution
- **Log Capture:** Application writes logs to a file for post-mortem analysis

## Test Architecture

Tests are divided into 3 projects, one for each host type:
1. `AdaptiveRemote.EndToEndTests.WpfHost` tests the primary `AdaptiveRemote` project. Requires Windows OS.
2. `AdaptiveRemote.EndToEndTests.ConsoleHost` tests the `AdaptiveRemote.Console` project. Requires Windows OS.
3. `AdaptiveRemote.EndToEndTests.HeadlessHost` tests the `AdaptiveRemote.Headless` project. Both Windows and Linux supported.

The three host projects are minimal, sharing most of their functionality from other projects.
- `AdaptiveRemote.EndToEndTests.Features` contains Gherkin `.feature` files that define test scenarios. 
These are compiled at build time into C# test classes that are built into the 3 assemblies, so that they all have the same tests.
- `AdaptiveRemote.EndToEndTests.Steps` contains definitions of the steps used in the feature scenarios.
These should also be minimal, just enough to translate steps into TestService calls and error check inputs.
- `AdaptiveRemote.EndToEndTests.TestServices` contains test services that load in the application being tested (the "host process")
as well as extension methods for the test service interfaces. The test services can interact directly with components in the host
process, but should also be fairly minimal, e.g. accessing a value or invoking a command. Extension methods will run in the test
process and can contain more complex logic, like waiting for a value to change, checking values, etc. Having this logic in the test
process makes it easier to debug. Services include:
	- [`ITestEndpoint`](../src/AdaptiveRemote.App/Services/Testing/ITestEndpoint.cs)
	  is the initial JSON-RPC interface exposed by the host for dynamically loading other test services.
	- [`ITestLogger`](../src/AdaptiveRemote.App/Services/Testing/ITestLogger.cs)
      is used to forward log messages from the test process to be included with host logs.
	- [`IApplicationTestService`](../src/AdaptiveRemote.App/Services/Testing/IApplicationTestService.cs)
	  has general application lifecycle methods.
	- [`IUITestService`](../src/AdaptiveRemote.App/Services/Testing/IUITestService.cs)
	  has methods for interacting with the UI, including button interactions and accessibility checking via axe-core.

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

### Accessibility Testing
- **Accessibility Contrast Checker:** Automated UI accessibility testing using [Deque axe-core](https://github.com/dequelabs/axe-core) via Playwright to validate WCAG 2 AA contrast requirements
- **Technology:** 
  - `Deque.AxeCore.Playwright` package integrates axe-core with Playwright's IPage API
  - Tests run via `IUITestService.CheckAccessibilityAsync()` which returns a list of violations
- **Test Coverage:**
  - Color contrast ratios (text and backgrounds)
  - WCAG 2 AA compliance (4.5:1 for normal text, 3:1 for large text)
  - Buttons use 36pt font, qualifying as "large text"
- **Implementation:**
  - Feature file: `AdaptiveRemote.EndToEndTests.Features/Accessibility.feature`
  - Step definitions: `AdaptiveRemote.EndToEndTests.Steps/AccessibilitySteps.cs`
  - Service method: `PlaywrightUITestService.CheckAccessibilityAsync()`
- **Running Tests:**
  ```bash
  dotnet test test/AdaptiveRemote.EndToEndTests.HeadlessHost \
      --filter "FullyQualifiedName~AccessibilityCompliance"
  ```
- **Notes:**
  - Tests work with all host types (WPF, Console, Headless)
  - Headless host is recommended for CI/CD as it requires no graphical environment
  - Violations include rule ID, impact level, description, help text, and HTML snippet
  - Tests protect against accessibility regressions in future development

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

### Linux Host (Headless)
- Executable has no .exe extension
- UI is hosted in a headless browser with Playwright tracing to record UI states

## Environment Requirements

### Linux CI
```bash
# Start virtual display
Xvfb :99 -screen 0 1024x768x24 &
export DISPLAY=:99

# Build Headless for Linux
dotnet build src/AdaptiveRemote.Headless/AdaptiveRemote.Headless.csproj -r linux-x64

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
