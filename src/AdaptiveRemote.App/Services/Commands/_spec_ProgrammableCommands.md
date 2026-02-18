# Programmable IR Commands � Design Document

# Goal
Enable users to program new IR commands through the application UI, making it accessible and easy to update remote layouts for new devices or functions.

# Reference documents
See `_doc_EndToEndTests.md` for details on writing integration tests.

# User Experience
- A small "Prog" button is added to the UI gutter. Clicking/tapping this toggles a global programming mode.
- In programming mode:
  - Only programmable commands (e.g., Broadlink IR commands) remain enabled; all others (e.g., TiVo, application commands) are visually disabled and unresponsive.
  - Clicking a programmable command starts the programming sequence for that button.
  - The UI displays a modal-style message (centered, not a separate window) with markdown support, instructing the user to point their remote at the Broadlink device and press the desired button.
  - The message display service supports a queue, but only one message is shown at a time.
- Programming mode is UI-only (not accessible via voice/Conversation system).
- The "Prog" button is the only non-disabled command in programming mode and toggles programming mode off when clicked again.

# Technical Design
## Model & Service Layer
- Extend the [`Command`](../../Models/Command.cs) model to support an optional `ProgramDelegate` action, similar to `ExecuteDelegate`.
- Only command services that support programming (initially, Broadlink) will provide a `ProgramDelegate` action.
- [`BroadlinkCommandService`](../../Services/Broadlink/BroadlinkCommandService.cs):
  - Implements the `ProgramDelegate` action by invoking the Broadlink device's IR learning protocol.
  - On successful learning, stores the new IR code in [`ProgrammaticSettings`](../../Services/ProgrammaticSettings/PersistSettings.cs) using a key like `IRData:CommandName` and a Base64-encoded value.
  - On startup, loads any programmed IR codes from [`ProgrammaticSettings`](../../Services/ProgrammaticSettings/PersistSettings.cs) and uses them in preference to hard-coded defaults.
- [`CommandServiceBase`](../../Services/CommandServiceBase.cs) provides a base wrapper for error handling, logging, and UI state for both `ExecuteDelegate` and `ProgramDelegate` actions.
- A global flag (e.g., on [`LifecycleView`](../../Models/LifecycleView.cs)) tracks whether programming mode is active.

## UI Layer
- Refactor UI components so that `ProgramButton` (to be implemented) is used instead of `CommandButton` when the application is in programming mode.
  - If possible, do not recreate the entire RemoteLayout tree structure. The UI should be the same up to the point where `CommandButton` or `ProgramButton` is selected.
- `ProgramButton`:
  - Enabled only if the command has a `ProgramDelegate` action.
  - Visual state indicates whether the command is programmed:
    - **Unprogrammed:** Outlined button, faded color, tooltip "Not programmed" <!-- TODO: Is there an icon we could use here to have consistent visual language with the programmed state? -->
    - **Programmed:** Solid color, checkmark icon, tooltip "Programmed"
    - **Disabled:** Grayed out, no interaction
  - Accessibility: high contrast, ARIA attributes, keyboard focus support.
- Modal message display service:
  - Used for both conversation and programming messages.
  - Accepts markdown for formatting (e.g., large title, smaller instructions).
  - Simple FIFO queue: only one message is shown at a time, subsequent messages are queued.
  - Prior to making these changes, there should be an integration test that activates listening mode and checks for the modal speech messages, to ensure that behavior does not regress.
  - The service should work through a View Model design pattern, similar to the way [`ConversationView`](../../Models/ConversationView.cs) is implemented, to allow for easy unit testing and separation of concerns.
  - Create `_doc_ModalMessages.md` for documentation of the modal message service and queueing requirements, e.g. that the application should avoid attempting to display multiple messages at the same time.

## Device Integration
- `BroadlinkCommandService` uses the Broadlink device's IR learning protocol to capture new commands.
- The protocol for learning IR codes must be implemented. The protocol is primarily defined by community Python projects (see [broadlink/broadlink](https://github.com/broadlink/broadlink)), as official documentation is lacking. The implementation must:
  - Initiate learning mode by sending a specific UDP packet to the device.
  - Wait for the device to respond, indicating it is ready to receive IR.
  - Receive the learned IR code as a packet, decode and store it.
  - Handle errors: device not found, timeout, protocol errors, user cancellation.

See [_doc_BroadlinkProtocol.md](../Broadlink/_doc_BroadlinkProtocol.md) for distilled protocol details, including packet types, sequence, and error handling.

## Storage of Programmed Commands
- Use [`ProgrammaticSettings`](../../Services/ProgrammaticSettings/PersistSettings.cs) to persistently store programmed IR commands.
- Keys are of the form `IRData:CommandName`, values are Base64-encoded IR data.
- This storage will replace hard-coded defaults in the code. Hard-coded commands must be removed and placed in a sample data file (`Settings.sample.ini`) for development and testing.

See [_doc_ProgrammaticSettings.md](../ProgrammaticSettings/_doc_ProgrammaticSettings.md) for file format details and sample files.

## Integration & Testing
- Test host configures a test-time `ProgrammaticSettings` data file with preprogrammed commands.
  - The path to this file is passed as a command line argument (overriding the default value)
  - The file should contain simple programmed payloads for some Broadlink commands, but not all
- Integration tests:
  - Confirm only programmed commands are enabled.
  - Confirm the correct value is sent to the simulated device.
  - Test programming mode: program an unprogrammed command, then execute it and verify the correct value is sent.
  - Test UI of programming mode: Only programmable commands and "Prog" are enabled; preprogrammed commands are visibly distinct from not-yet-programmed commands.
  - Test device errors, timeouts, and user cancellation:
    - Cancellation: user can dismiss the modal or toggle programming mode off; command returns to unprogrammed state.
    - Device errors: simulate device not found, protocol errors, and verify UI and error handling.
    - Timeout: simulate device timeout and verify UI and error handling.
- `SimulatedBroadlinkDevice` must implement the programming (learning) sequence for tests, matching the user experience and protocol described above.
- Integration tests should be written in Gherkin, using step definitions that mirror user actions:
  - When I click the "Prog" button
  - Then I should see "Power" is enabled and unprogrammed
  - When I click the "Power" button
  - And I use my remote control to send a Power command to the Broadlink device
  - Then I should see (expected UI changes)
  - When I cancel programming mode
  - Then the modal closes and the button returns to unprogrammed state

### ADR-149: Speech test service for integration tests

Integration tests need a way to trigger the conversation UI's listening lifecycle (wake word and stop-listening) without playing or capturing real audio. The test design is to provide a test-time implementation of `ISpeechRecognitionEngine` that tests can register before the app host is built.

Key points:
- A new test service implements `ISpeechRecognitionEngine` and keeps the same `SpeechRecognized`/`SpeechRejected` events so application code is unchanged.
- It exposes test-only methods such as `RaiseRecognized(string text, int confidence = 80, params (string key, string value)[] semantics)`, `RaiseRejected(string text, int confidence = 0)`. Convenience helpers like `Speak(string phrase)` will map known phrases to semantics like `("system","STARTLISTENING")` and `("system","STOPLISTENING")`. This will be called by a Gherkin step like `When I say {string}`, passing the `string` parameter into the helper function. 
- This requires a new application startup flow, so that the test can override services like `ISpeechRecognitionService` in the context of running tests. The new flow will work as follows:
  1. The new [`AppHostRunner`](../../AppHostRunner.cs) encapsulates the startup process. It is subclassed in different hosts for host-specific configuration, following the existing pattern.
       a. AcceleratedServices: Early services created to display the UI and basic status messages or errors immediately on startup. This will now include the test endpoint, if it is configured.
       b. Create `IHostBuilder`; hosts can add host-specific services to the builder prior to handing it back to `AppHostRunner`.
       c. Add AcceleratedServices to the `IHostBuilder` as instance services.
       d. Add settings configuration and shared services to the `IHostBuilder`
  2. After configuring the `IHostBuilder` (settings, services, etc.), it will call [`ITestEndpointHooks`](..\Testing\ITestEndpointHooks.cs) to configure test-only services
       a. For normal execution, a no-op implementation returns immediately, unblocking startup
       b. For test execution, the test endpoint blocks startup and waits for the test process to load test services.
          When the test is done adding services, it signals the host process to continue, which unblocks the startup sequence.
  3. After the test hooks return, the `IHost` is built using the configured `IHostBuilder`.
  4. The `IHost.Services` collection is passed to the `ITestEndpointHooks` implementation, so that test services can be created from the same service provider and have access to the same dependencies as application services.
      a. For normal execution, this is another no-op
      b. For test execution this unblocks the `ITestEndpoint` from returning an `ITestServicesProvider` to the test process.
  5. The `IHost` is run and the application starts as normal, but with test services registered and available for use in tests.
- From the test side, the flow is:
  1. Start the host process using the command line
  2. Wait for the test endpoint, and connect to it to get an `ITestEndpoint` instance
  3. Provide test service additions/overrides via the `ITestEndpoint` API. These services are passed to the `ITestEndpointHooks` implementation in the host process and registered before the host is built.
  4. Signal the host process to continue startup once all test services are registered.
  5. Request the `ITestServiceProvider` instance from the `ITestEndpoint`. This service is created from the host's service provider, so that any dependencies are fulfilled.
- To support this, the existing `ITestEndpoint` service will be broken into two contracts, as described below.

```CSharp
public interface ITestEndpoint
{
    /// <summary>
    /// Loads a test service into the host's service collection.
    /// </summary>
    Task AddTestServiceAsync(string contractType, string serviceName, string serviceAssembly, CancellationToken cancellationToken);
    
    /// <summary>
    /// Unblocks the host process's startup sequence.
    /// </summary>
    Task BuildAndRunHostAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Attempt to cleanly shut down the host process. 
    /// If the host has been run, this should cause a normal shutdown via `IHostApplicationLifetime`.
    /// Otherwise, this should abort the startup sequence.
    /// </summary>
    Task StopApplicationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Requests an API for loading test services and retrieving constract interfaces.
    /// This can only be called after BuildAndRunHostAsync, since the host's service provider is not available until then.
    /// </summary>
    Task<ITestServiceProvider> GetTestServiceProviderAsync(CancellationToken cancellationToken);
}
```

```CSharp
public interface ITestServiceProvider
{
    /// <summary>
    /// Dynamically loads a test service from the specified assembly and type.
    /// The test service is instantiated within the application's DI scope to access scoped services.
    /// </summary>
    /// <param name="assemblyPath">Full path to the assembly containing the test service type.</param>
    /// <param name="typeName">Fully qualified name of the test service type to instantiate.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A proxy to the test service that can be used to invoke test commands.</returns>
    Task<IApplicationTestService> CreateTestServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken);

    /// <summary>
    /// Dynamically loads a test logger from the specified assembly and type.
    /// The test logger is instantiated within the application's DI scope so it can access scoped services
    /// and forward log events back to the host test harness.
    /// </summary>
    /// <param name="assemblyPath">Full path to the assembly containing the test logger type.</param>
    /// <param name="typeName">Fully qualified name of the test logger type to instantiate.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A proxy to the test logger that can be used by tests to emit or collect log events.</returns>
    Task<ITestLogger> CreateTestLoggerAsync(string assemblyPath, string typeName, CancellationToken cancellationToken);

    /// <summary>
    /// Dynamically loads a UI test service from the specified assembly and type.
    /// The UI test service is instantiated within the application's DI scope so it can access
    /// Playwright/WebView2 objects and interact with the UI.
    /// </summary>
    /// <param name="assemblyPath">Full path to the assembly containing the UI test service type.</param>
    /// <param name="typeName">Fully qualified name of the UI test service type to instantiate.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A proxy to the UI test service that can be used to interact with the UI.</returns>
    Task<IUITestService> CreateUITestServiceAsync(string assemblyPath, string typeName, CancellationToken cancellationToken);
}
```

## Accessibility & Platform Notes
- All UI changes must maintain or improve accessibility (vision, speech, eye-gaze, keyboard/mouse).
- The solution targets Windows (.NET 10, WPF with Blazor WebView) and Linux (Headless Host).


## Work Items and Exit Criteria

## [Epic ADR-127](https://jodasoft.atlassian.net/browse/ADR-127). Programmable IR commands
- Parent for all Task work items below.
- Defines a user experience for reading IR command codes using the Broadlink device, and mapping those codes to commands. 

### [Task ADR-149](https://jodasoft.atlassian.net/browse/ADR-149). Speech test service

- Provide a test-time `ISpeechRecognitionEngine` replacement that exposes methods to raise `SpeechRecognized`/`SpeechRejected` events (including wake-word and stop-listening semantics) so ADR-141 integration tests can deterministically trigger the conversation modal UI.
- **Exit Criteria:** 
    - There is a basic integration test that verifies the functionality of the test-time `ISpeechRecognitionEngine`. This test can be removed later but should validate that the new test service is ready for new tests to be created.
    - Application startup still functions correctly with the new startup flow, and the test service can be registered without affecting normal execution.
    - This service must be available for all hosts (headless and WPF). Use the `HostRunner`/`AddAcceleratedService` pattern so tests can register replacements consistently across host types.

### [Task ADR-141](https://jodasoft.atlassian.net/browse/ADR-141). Integration Test for Existing Conversation Modal Message UI
- Add an integration test that activates listening mode and verifies the modal message UI for conversation/speech is displayed as expected.
  - Activate listening mode by clicking on the text 'Say "Hey Remote" to get my attention' in the UI
  - Validate the message by looking at the HTML in the UI. This will make the test resilient when the ViewModel or Razor components change.
  - Click on the text 'I'm listening...' to deactivate listening mode, make sure the modal message is dismissed
- **Exit Criteria:** Test passes and covers a basic modal message scenario.

### [ADR-142](https://jodasoft.atlassian.net/browse/ADR-142): Refactor Modal Message System for Conversation and Programming
- Refactor the modal message system to support both conversation and programming messages, with markdown formatting and message queuing.
- Update the conversation system to use the new message system and ensure no regression in existing behavior.
- **Exit Criteria:**
  - Unit tests for the message system (covering queueing, markdown rendering, and message replacement).
  - All existing conversation modal message integration tests pass.

### [ADR-148](https://jodasoft.atlassian.net/browse/ADR-148): Move Hard-Coded IR Payloads to ProgrammaticSettings with Migration
- Update IR command loading to use ProgrammaticSettings for IR payloads, disabling commands not present in the settings.
- Implement a migration/bootstrap mechanism to populate ProgrammaticSettings with current hard-coded values if missing.
- **Exit Criteria:**
  - Unit tests for migration logic and fallback behavior.
  - Integration tests set up a ProgrammaticSettings file with some programmed commands and verify that:
	- Only programmed commands are enabled.
	- The correct IR payloads from the settings file are sent to the simulated device.

### [ADR-146](https://jodasoft.atlassian.net/browse/ADR-146): Refactor UI Components for Programming Mode
- Create a new `ProgramButton` component and integrate it into the UI, replacing `CommandButton` when the application is in Programming mode.
- Implement UI state management for programming mode, including enabling/disabling and visual distinction for programmed/unprogrammed commands.
- Add the "Prog" button to the UI and ensure it toggles programming mode.
- **Exit Criteria:**
  - Integration test verifies correct UI state in and out of programming mode.

### [ADR-147](https://jodasoft.atlassian.net/browse/ADR-147): CommandServiceBase Support for ProgramDelegate
- Extend [`CommandServiceBase`](../../Services/CommandServiceBase.cs) to support a `ProgramDelegate` action, with error handling, logging, and UI state management similar to `ExecuteDelegate`.
- The exception is that `ProgramDelegate` is only available for command services that support programming (initially, Broadlink), so the base class should handle the case where the subclasses do not provide a `ProgramDelegate`.
- **Exit Criteria:**
  - Unit tests for new logic in `CommandServiceBase`.

### [ADR-144](https://jodasoft.atlassian.net/browse/ADR-144): BroadlinkCommandService Support for ProgramDelegate
- Implement the `ProgramDelegate` action in [`BroadlinkCommandService`](../../Services/Broadlink/BroadlinkCommandService.cs), including IR learning protocol, display message, and error handling.
- `ProgramDelegate` will use new methods on [`IUdpService`](../../Services/Broadlink/IUdpService.cs) and other services. Implement necessary methods and Packet types to support the learning protocol.
- **Exit Criteria:**
  - Unit tests for Broadlink programming logic, including error and edge cases. (Cancellation, timeout, protocol errors, internal errors)
  - Unit tests for all new packet types and service methods, including error and edge cases.

### [ADR-145](https://jodasoft.atlassian.net/browse/ADR-145): End-to-End Programming Feature with Integration Tests
- Implement the full programming workflow: entering programming mode, programming a command, verifying execution, and handling errors/edge cases (e.g., device not found, user cancels, timeout, mode toggled off mid-sequence).
- SimulatedBroadlinkDevice implements the learning protocol for tests.
- **Exit Criteria:**
  - Integration tests cover the happy path and all major error/edge cases.
  - All tests pass in both Windows and Headless (Linux) test environments.

### [ADR-143](https://jodasoft.atlassian.net/browse/ADR-143): Update documentation for Programmable IR Commands
- Rename `_spec_ProgrammableCommands.md` to `_doc_ProgrammableCommands.md` and update its content with the final design of the feature
- Update any other `_doc_*.md` files in this repo with relevant changes to the systems they describe.
- Update `README.md` with links to all new documentation in the repo.
- Make sure that all new interfaces and APIs have XML doc comments explaining how to use them.
- **Exit Criteria:**
  - Human developers and LLM agents have architectural descriptions and inline documentation that can be used for future development
  - Documentation should link to source files rather than duplicating information (interfaces or algorithms) that can be read from source files. Documentation should not duplicate information that can end up out of date with the source files.
