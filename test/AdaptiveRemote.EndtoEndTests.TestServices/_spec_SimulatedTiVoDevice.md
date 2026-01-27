# Simulated devices for testing

Issue: ADR-120 — Simulated TiVo device for testing

--

**Scope:**
- Provide a locally runnable simulated TiVo device that speaks the same TCP-based protocol used by the real TiVo device so end-to-end tests can validate messages and behaviors.


**Goal:**
- Replace the current `NullCommandService`-based tests with a deterministic, inspectable test device that records and validates messages from the application under test.

**Background:**
- Current tests use `NullCommandService` (via the `--tivo:Fake=True` command line argument) which cannot validate real protocol-level interactions. A simulated device will allow higher-fidelity E2E tests for device-specific flows.
- Related issues: ADR-120 (this doc).

**Assumptions & Constraints:**
 - Runs on Windows and Linux test hosts; should run in-process in the test process.
 - Communication with the application is TCP-based and should accept/emit the same messages as the real device.
 - Record messages via the project's logging pipeline (`ILogger`); CI captures test logs so no custom JSON persistence is required.
 - Accessibility, security, and platform compatibility follow existing test-host constraints from the project.
 - Concrete constraints:
   - Bind TCP listener to loopback only by default.
   - No admin/elevated privileges required to run.
   - Do not write artifacts outside of test-controlled directories.
   - Support Windows and Linux test runners used by CI; avoid native-only dependencies.

**Functional Requirements:**
1. The simulator exposes a configurable TCP endpoint (port selectable per-test).
2. It accepts connections and parses incoming messages according to the TiVo protocol used by the app.
3. It records all inbound and outbound messages with timestamps for later inspection.
4. Scripted responses are out of scope for the initial implementation and may be added later if needed.
5. It provides a synchronous in-process .NET control API for tests to query recorded messages.
6. It runs embedded inside the test process (in-process only). Step/BeforeScenario bindings will be responsible for starting and stopping devices.

**Non‑Functional Requirements:**
- Deterministic behavior under tests (configurable timeouts and delays).
- Lightweight; start/stop quickly to keep test run-time low.
- Secure by default: bind to loopback only unless explicitly configured.
- Logging must integrate with existing test logs and be easy to attach on CI failures.

**Acceptance Criteria:**
- A test can start the simulator, perform an application flow, and then assert that a specific message was received within a timeout.
- Recorded message payloads are accessible to the test and match expected protocol fields.
- Simulator supports injecting a scripted response and the application under test reacts as if a real device responded.

**Design / Architecture (high-level):**
- Component: `SimulatedTiVoDevice`
  - TCP listener on configured port
  - Message parser/serializer matching the TiVo protocol (derive behavior from `AdaptiveRemote.App/Services/TiVo/TiVoService.cs`).
  - Recorder store (in-memory) and surface messages via `ILogger` so existing test logs capture them.
  - Control API: in-process .NET API (synchronous) for test orchestration
- Component: `TestEnvironment`
  - A high-level manager for test device builders and running devices.
  - Implementation will live in the Host folder of `AdaptiveRemote.EndToEndTests.TestServices` so the test host builder can compose devices and the Step/BeforeScenario bindings can start/stop them.

The following is a C# API that follows a DeviceBuilder pattern (preferred). Builders are responsible for construction and starting; started devices expose only synchronous methods for test use. Long-running or async work may be wrapped by `WaitHelpers.WaitForAsyncTask` in the implementation.

```csharp
// Minimal representation of a recorded message (line-based ASCII payload)
public sealed record RecordedMessage
{
  public DateTimeOffset Timestamp { get; init; }
  public string Payload { get; init; } = string.Empty; // raw ASCII line
  public bool Incoming { get; init; } // true if received from the app
}

// Represents a started, running test device. Methods are synchronous.
public interface ITestDevice : IDisposable
{
  // Stop the device and release resources. Safe to call multiple times.
  void Stop();

  // The TCP port the device is listening on. Valid while device is running.
  int Port { get; }

  // Return a copy of recorded messages observed by the device.
  IReadOnlyList<RecordedMessage> GetRecordedMessages();

  // Clear the messages that have been recorded so far, to prepare for the next test
  void ClearRecordedMessages();
}

// Builder pattern: configure and start a device. Start returns a running ITestDevice.
public interface ITestDeviceBuilder : IDisposable
{
  // Optionally configure a preferred port (0 for ephemeral).
  ITestDeviceBuilder WithPort(int port);

  // Start the device synchronously and return the running device.
  ITestDevice Start();
}

// Simple TestEnvironment manager to hold builders/devices for a test run
public interface ITestEnvironment : IDisposable
{
  // Register device builder
  void RegisterDevice(string name, ITestDeviceBuilder builder);

  // Start a named device and return the running device
  ITestDevice StartDevice(string name);

  // Retrieve a running device
  bool TryGetDevice(string name, out ITestDevice? device);
}
```

**Implementation Plan (proposed tasks):**
1. Create `SimulatedTiVoDevice` in a new folder within the TestServices project: `SimulatedTiVo`
2. Implement a minimal TCP listener and a simple message parser that accepts TiVo command messages (derive behavior from the client code in `AdaptiveRemote.App/Services/TiVo/TiVoService.cs`).
3. Add recorder and control API (`ITestDevice`) that exposes recorded messages for verification and logs messages via `ILogger`.
4. Add `TestEnvironment` to the Host folder of `AdaptiveRemote.EndToEndTests.TestServices` to manage simulated devices; Step/BeforeScenario bindings will call into it to start/stop devices.
5. Add step bindings and test hooks to `AdaptiveRemote.EndToEndTests.Steps` to register/start/stop devices.
6. Add Feature files to `AdaptiveRemote.EndToEndTests.Features` for new TiVo scenarios (Gherkin steps already proposed in the test suite).

**Proposed Gherkin test steps:**
  Given there is a simulated TiVo device               #Ensure the TiVo simulator is running
  And the application is in the Ready state            #Ensure the application is started and ready; shut down and restart if it's in an error state
  When I click on the Play button                      #Step already exists
  Then I should see the TiVo receives a Play message   #Check the TiVo simulator for the expected message

**Risks & Mitigations:**
- Risk: Flaky timing interactions. Mitigation: Expose configurable delays/timeouts and use WaitHelpers.ExecuteWithRetries to poll for assertions. See `IApplicationTestServiceExtensions.WaitForPhase` as an example.


