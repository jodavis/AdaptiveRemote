# Simulated TiVo Device for Testing

Issue: ADR-120 — Simulated TiVo device for testing

## Overview

The simulated TiVo device provides a locally runnable test double that implements the TiVo TCP-based protocol, enabling end-to-end tests to validate TiVo device interactions without requiring physical hardware.

## Architecture

### Components

#### SimulatedTiVoDevice
The core TCP server that listens for connections and records incoming messages.

**Location:** `test/AdaptiveRemote.EndtoEndTests.TestServices/SimulatedTiVo/SimulatedTiVoDevice.cs`

**Key Features:**
- Binds to loopback (127.0.0.1) by default for security
- Supports ephemeral ports (port 0) for parallel test execution
- Records all incoming messages with timestamps
- Implements the TiVo line-based ASCII protocol (messages terminated with `\r`)
- Thread-safe message recording using `ConcurrentBag`
- Async TCP connection handling
- Clean shutdown support

#### TestEnvironment
Manages the lifecycle of simulated devices during test runs.

**Location:** `test/AdaptiveRemote.EndtoEndTests.TestServices/Host/TestEnvironment.cs`

**Key Features:**
- Device registration and lifecycle management
- Name-based device lookup
- Automatic cleanup on disposal
- Builder pattern support

#### Test Steps
Integration with Reqnroll test framework via step definitions.

**Location:** `test/AdaptiveRemote.EndToEndTests.Steps/TiVoSteps.cs`

**Key Features:**
- `@tivo` tag support for scenarios requiring simulated device
- Automatic device startup before host initialization
- Message verification with configurable timeout
- Clear error messages with recorded message history

### Interfaces

```csharp
// Base recorded message structure
public sealed record RecordedMessage
{
    public DateTimeOffset Timestamp { get; init; }
    public string Payload { get; init; }
    public bool Incoming { get; init; }
}

// Running device interface
public interface ITestDevice : IDisposable
{
    void Stop();
    int Port { get; }
    IReadOnlyList<RecordedMessage> GetRecordedMessages();
    void ClearRecordedMessages();
}

// Device builder interface
public interface ITestDeviceBuilder : IDisposable
{
    ITestDeviceBuilder WithPort(int port);
    ITestDevice Start();
}

// Test environment interface
public interface ITestEnvironment : IDisposable
{
    void RegisterDevice(string name, ITestDeviceBuilder builder);
    ITestDevice StartDevice(string name);
    bool TryGetDevice(string name, out ITestDevice? device);
}
```

## Protocol Implementation

### TiVo TCP Protocol
- **Port:** Configurable (default 31339 for real devices; ephemeral for tests)
- **Transport:** TCP over loopback
- **Message Format:** Line-based ASCII with `\r` (carriage return) terminator
- **Command Format:** `IRCODE {command}\r` (e.g., `IRCODE PLAY\r`)

### Key Implementation Details

**Line Terminator Handling:**
The TiVo protocol uses `\r` (carriage return) as the line terminator, not the standard `\n` (line feed) or `\r\n` (carriage return + line feed). This required a custom `ReadLineAsync` implementation to properly detect end-of-line.

**Message Recording:**
All incoming messages are recorded immediately upon receipt with:
- UTC timestamp
- Raw payload (without line terminator)
- Direction flag (incoming vs. outgoing)

## Usage

### Basic Test Scenario

```gherkin
@tivo
Feature: TiVo Device Integration
  Scenario: TiVo receives Play command
    Given there is a simulated TiVo device
    And the application is not running
    When I start the application
    Then I should see the application in the Ready phase
    When I click on the 'Play' button
    Then I should see the TiVo receives a "PLAY" message
```

### Test Step Definitions

```csharp
[Given(@"there is a simulated TiVo device")]
public void GivenThereIsASimulatedTiVoDevice()
{
    // Device is automatically started via BeforeScenario hook
    // This step just verifies it's running
}

[Then(@"I should see the TiVo receives a {string} message")]
public void ThenIShouldSeeTheTiVoReceivesAMessage(string expectedCommand)
{
    // Polls for message with 5-second timeout
    // Returns detailed error with all recorded messages on failure
}
```

### Configuration

The simulated device is automatically configured when a test scenario is tagged with `@tivo`:

1. **BeforeScenario (Order=50):** TestEnvironment is created
2. **BeforeScenario (Order=100, @tivo):** Simulated TiVo device is started on ephemeral port
3. **BeforeScenario (Order=200):** Host is started with `--tivo:IP=127.0.0.1:{port}` argument
4. **AfterScenario:** Cleanup of devices and host

## Testing

### Running E2E Tests

```bash
# Build headless host for Linux
dotnet build src/AdaptiveRemote.Headless/AdaptiveRemote.Headless.csproj -r linux-x64

# Install Playwright browsers (one-time)
pwsh src/AdaptiveRemote.Headless/bin/Debug/net8.0/playwright.ps1 install chromium

# Run all headless E2E tests
dotnet test test/AdaptiveRemote.EndToEndTests.HeadlessHost/AdaptiveRemote.EndToEndTests.HeadlessHost.csproj
```

### Test Results

As of ADR-120 implementation:
- ✅ Application startup and shutdown without errors
- ✅ TiVo receives Play command

Both tests pass consistently on Linux (Ubuntu) with Playwright headless browser.

## Design Decisions

### Why In-Process?
Running the simulated device in the test process (rather than as a separate service) provides:
- Simpler lifecycle management
- No inter-process communication overhead
- Direct access to recorded messages
- Easier debugging

### Why Loopback Only?
Binding to loopback (127.0.0.1) by default ensures:
- No firewall configuration required
- No security risks from external connections
- Consistent behavior across environments

### Why Ephemeral Ports?
Using port 0 (ephemeral) by default enables:
- Parallel test execution without port conflicts
- No need for port coordination between tests
- CI/CD pipeline compatibility

### Why ConcurrentBag for Message Recording?
`ConcurrentBag<T>` provides:
- Thread-safe message recording
- Low contention for add operations
- Simple API for test verification

## Known Limitations

1. **No Response Simulation:** The current implementation only records incoming messages. It does not send responses back to the client. This is sufficient for testing command transmission but not for testing response handling.

2. **Single Connection:** While the device accepts multiple connections sequentially, it does not handle multiple simultaneous connections. This is not a limitation for current test scenarios.

3. **No Message Replay:** Messages are only recorded, not replayed. Tests must poll for messages within the assertion timeout.

## Future Enhancements

- **Response Simulation:** Add support for scripted responses to enable testing of bidirectional communication
- **Message Filtering:** Add query methods for filtering recorded messages by timestamp, content, or pattern
- **Connection Metrics:** Track connection count, duration, and bandwidth for performance testing
- **Multi-Device Support:** Extend TestEnvironment to support multiple device types (e.g., Broadlink)

## References

- TiVo Protocol: Uses I8Beef.TiVo library implementation as reference
- Test Framework: Reqnroll (SpecFlow successor) with MSTest
- Original Specification: `test/AdaptiveRemote.EndtoEndTests.TestServices/_spec_SimulatedTiVoDevice.md` (superseded by this document)
