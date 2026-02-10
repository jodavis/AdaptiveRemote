# Programmable IR Commands – Design Document

## Goal
Enable users to program new IR commands through the application UI, making it accessible and easy to update remote layouts for new devices or functions.

## Reference documents
See `_doc_EndToEndTests.md` for details on writing integration tests.

## User Experience
- A small "Prog" button is added to the UI gutter. Clicking/tapping this toggles a global programming mode.
- In programming mode:
  - Only programmable commands (e.g., Broadlink IR commands) remain enabled; all others (e.g., TiVo, application commands) are visually disabled and unresponsive.
  - Clicking a programmable command starts the programming sequence for that button.
  - The UI displays a modal-style message (centered, not a separate window) with markdown support, instructing the user to point their remote at the Broadlink device and press the desired button.
  - The message display service supports a queue, but only one message is shown at a time.
- Programming mode is UI-only (not accessible via voice/Conversation system).
- The "Prog" button is the only non-disabled command in programming mode and toggles programming mode off when clicked again.

## Model & Service Layer
- Extend the [`Command`](../../Models/Command.cs) model to support an optional `ProgramDelegate` action, similar to `ExecuteDelegate`.
- Only command services that support programming (initially, Broadlink) will provide a `ProgramDelegate` action.
- [`BroadlinkCommandService`](../../Services/Broadlink/BroadlinkCommandService.cs):
  - Implements the `ProgramDelegate` action by invoking the Broadlink device's IR learning protocol.
  - On successful learning, stores the new IR code in [`ProgrammaticSettings`](../../Services/ProgrammaticSettings/PersistSettings.cs) using a key like `IRData:CommandName` and a Base64-encoded value.
  - On startup, loads any programmed IR codes from [`ProgrammaticSettings`](../../Services/ProgrammaticSettings/PersistSettings.cs) and uses them in preference to hard-coded defaults.
- [`CommandServiceBase`](../../Services/CommandServiceBase.cs) provides a base wrapper for error handling, logging, and UI state for both `ExecuteDelegate` and `ProgramDelegate` actions.
- A global flag (e.g., on [`LifecycleView`](../../Models/LifecycleView.cs)) tracks whether programming mode is active.

- Refactor UI components so that `ProgramButton` (to be implemented) is used for programmable commands instead of `CommandButton`.
  - If possible, do not recreate the entire RemoteLayout tree structure. The UI should be the same up to the point where `CommandButton` or `ProgramButton` is selected.
- `ProgramButton`:
  - Enabled only if the command has a `ProgramDelegate` action.
  - Visual state indicates whether the command is programmed (distinct from simply enabled/disabled).
- Modal message display service:
  - Used for both conversation and programming messages.
  - Accepts markdown for formatting (e.g., large title, smaller instructions).
  - Queues messages if one is already being displayed.
  - Prior to making these changes, there should be an integration test that activates listening mode and checks for the modal speech messages, to ensure that behavior does not regress.
  - The service should work through a View Model design pattern, similar to the way [`ConversationView`](../../Models/ConversationView.cs) is implemented, to allow for easy unit testing and separation of concerns.

## Device Integration
- `BroadlinkCommandService` uses the Broadlink device's IR learning protocol to capture new commands.
- The protocol for learning IR codes must be implemented (see Broadlink device documentation for details).

## Storage of Programmed Commands
- Use [`ProgrammaticSettings`](../../Services/ProgrammaticSettings/PersistSettings.cs) to persistently store programmed IR commands.
- Keys are of the form `IRData:CommandName`, values are Base64-encoded IR data.
- On startup, programmed values override any hard-coded defaults for IR commands.

## Integration & Testing
- Test host configures a test-time `ProgrammaticSettings` file with preprogrammed commands.
  - The path to this file is passed as a command line argument (overriding the default value)
  - The file should contain simple programmed payloads for some Broadlink commands, but not all
- Integration tests:
  - Confirm only programmed commands are enabled. (Make sure only programmed commands are enabled)
  - Confirm the correct value is sent to the simulated device. (Confirm the payloads from the settings file)
  - Test programming mode: program an unprogrammed command, then execute it and verify the correct value is sent.
  - Test UI of programming mode: Only programmable commands and "Prog" are enabled; preprogrammed commands are visibly distinct from not-yet-programmed commands.
- `SimulatedBroadlinkDevice` must implement the programming (learning) sequence for tests.
  - See Broadlink device documentation for the learning protocol.

## Accessibility & Platform Notes
- All UI changes must maintain or improve accessibility (vision, speech, eye-gaze, keyboard/mouse).
- The solution targets Windows (.NET 10, WPF with Blazor WebView) and Linux (Headless Host).


## Work Items and Exit Criteria

## [Epic ADR-127](https://jodasoft.atlassian.net/browse/ADR-127). Programmable IR commands
- Parent for all Task work items below.
- Defines a user experience for reading IR command codes using the Broadlink device, and mapping those codes to commands. 

### [Task ADR-141](https://jodasoft.atlassian.net/browse/ADR-141). Integration Test for Existing Conversation Modal Message UI
- Add an integration test that activates listening mode and verifies the modal message UI for conversation/speech is displayed as expected.
  - Activate listening mode by clicking on the text 'Say "Hey Remote" to get my attention' in the UI
  - Validate the message by looking at the HTML in the UI. This will make the test resilient when the ViewModel or Razor components change.
  - Click on the text 'I'm listening...' to deactivate listening mode, make sure the modal message is dismissed
- **Exit Criteria:** Test passes and covers a basic modal message scenario.

### [ADR-142](https://jodasoft.atlassian.net/browse/ADR-142) Refactor Modal Message System for Conversation and Programming
- Refactor the modal message system to support both conversation and programming messages, with markdown formatting and message queuing.
- Update the conversation system to use the new message system and ensure no regression in existing behavior.
- **Exit Criteria:**
  - Unit tests for the message system (covering queueing, markdown rendering, and message replacement).
  - All existing conversation modal message integration tests pass.

### 3. Move Hard-Coded IR Payloads to ProgrammaticSettings with Migration
- Update IR command loading to use ProgrammaticSettings for IR payloads, disabling commands not present in the settings.
- Implement a migration/bootstrap mechanism to populate ProgrammaticSettings with current hard-coded values if missing.
- **Exit Criteria:**
  - Unit tests for migration logic and fallback behavior.
  - Integration tests set up a ProgrammaticSettings file with some programmed commands and verify that:
	- Only programmed commands are enabled.
	- The correct IR payloads from the settings file are sent to the simulated device.

### [ADR-146](https://jodasoft.atlassian.net/browse/ADR-146) Refactor UI Components for Programming Mode
- Create a new `ProgramButton` component and integrate it into the UI, replacing `CommandButton` when the application is in Programming mode.
- Implement UI state management for programming mode, including enabling/disabling and visual distinction for programmed/unprogrammed commands.
- Add the "Prog" button to the UI and ensure it toggles programming mode.
- **Exit Criteria:**
  - Integration test verifies correct UI state in and out of programming mode.

### [ADR-147](https://jodasoft.atlassian.net/browse/ADR-147) CommandServiceBase Support for ProgramDelegate
- Extend [`CommandServiceBase`](../../Services/CommandServiceBase.cs) to support a `ProgramDelegate` action, with error handling, logging, and UI state management similar to `ExecuteDelegate`.
- The exception is that `ProgramDelegate` is only available for command services that support programming (initially, Broadlink), so the base class should handle the case where the subclasses do not provide a `ProgramDelegate`.
- **Exit Criteria:**
  - Unit tests for new logic in `CommandServiceBase`.

### [ADR-144](https://jodasoft.atlassian.net/browse/ADR-144) BroadlinkCommandService Support for ProgramDelegate
- Implement the `ProgramDelegate` action in [`BroadlinkCommandService`](../../Services/Broadlink/BroadlinkCommandService.cs), including IR learning protocol, display message, and error handling.
- `ProgramDelegate` will use new methods on [`IUdpService`](../../Services/Broadlink/IUdpService.cs) and other services. Implement necessary methods and Packet types to support the learning protocol.
- **Exit Criteria:**
  - Unit tests for Broadlink programming logic, including error and edge cases. (Cancellation, timeout, protocol errors, internal errors)
  - Unit tests for all new packet types and service methods, including error and edge cases.

### [ADR-145](https://jodasoft.atlassian.net/browse/ADR-145) End-to-End Programming Feature with Integration Tests
- Implement the full programming workflow: entering programming mode, programming a command, verifying execution, and handling errors/edge cases (e.g., device not found, user cancels, timeout, mode toggled off mid-sequence).
- SimulatedBroadlinkDevice implements the learning protocol for tests.
- **Exit Criteria:**
  - Integration tests cover the happy path and all major error/edge cases.
  - All tests pass in both Windows and Headless (Linux) test environments.

### [ADR-143](https://jodasoft.atlassian.net/browse/ADR-143) Update documentation for Programmable IR Commands
- Rename `_spec_ProgrammableCommands.md` to `_doc_ProgrammableCommands.md` and update its content with the final design of the feature
- Update any other `_doc_*.md` files in this repo with relevant changes to the systems they describe.
- Update `README.md` with links to all new documentation in the repo.
- Make sure that all new interfaces and APIs have XML doc comments explaining how to use them.
- **Exit Criteria:**
  - Human developers and LLM agents have architectural descriptions and inline documentation that can be used for future development
  - Documentation should link to source files rather than duplicating information (interfaces or algorithms) that can be read from source files. Documentation should not duplicate information that can end up out of date with the source files.
