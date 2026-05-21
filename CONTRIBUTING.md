# Contributing to AdaptiveRemote

> If you are planning, writing, or reviewing code, read the [Code Guidelines](#code-guidelines) section below.

Thank you for your interest in contributing to AdaptiveRemote! This project aims to provide
accessible remote control solutions for users with limited or no mobility, with a focus on
vision accessibility, speech recognition, and eye-gaze input.

## How to Contribute

- **Open an Issue First:** Please open an issue before submitting code. Use the provided 
templates for [bug reports](../.github/ISSUE_TEMPLATE/bug_report.md) and 
[feature requests](../.github/ISSUE_TEMPLATE/feature_request.md).
- **Development Workflow:**
  - Fork the repository and create a feature branch for your changes.
  - Ensure all unit tests pass before submitting a pull request.
  - Code reviews are required before merging (self-review is acceptable for solo developers).
- **Coding Standards:**
  - Follow the code style and naming conventions defined in the `.editorconfig` file.
  - Write clear, maintainable, and well-documented code.
- **Testing:**
  - Add or update unit tests as appropriate for your changes.
  - All tests must pass before your pull request will be considered.
- **Documentation:**
  - Architecture and design notes are stored alongside implementations using `_doc_*.md` filenames so they surface at the top of each folder.
  - Living documentation files should:
    - Focus on high-level architecture, design intent, and non-obvious decisions.
    - Avoid implementation details that are likely to change; refer to source code for specifics.
    - Link to relevant source files for details, and use comments in source files for non-obvious implementation details.
    - Be LLM-friendly, using clear language and structure to assist coding agents and future contributors.
  - When designs are updated, documents should be updated to match.
  - When new subsystems are added, they should include a documentation file.
- **Accessibility:**
  - Prioritize vision accessibility, speech recognition, and eye-gaze input.
  - Keyboard/mouse accessibility is less critical, but do not introduce regressions.
  - Note any accessibility testing or considerations in your pull request.
- **Commit Messages:**
  - Use clear, descriptive commit messages that explain what and why you changed.
  - No formal convention is required, but clarity is appreciated.
- **Supported Platforms:**
  - The application targets Windows OS and .NET 10.
  - Required NuGet packages are restored during build.
- **Contact:**
  - For questions or support, open an issue and @jodavis.

---

## Code Guidelines

### Testing

#### Naming

`ClassName_Method_Scenario_ExpectedResult`

Example: `TiVoService_InitializeAsync_WaitsForTiVoLocator`

#### Structure

Use AAA (Arrange-Act-Assert). Use `[TestInitialize]` for mock setup and `[TestCleanup]` for
mock verification. Group setup calls into `Expect_*` helper methods.

#### Mocks

- Create `Mock<T>` objects as `private readonly` fields on the test class so they are shared
  across setup helpers, test methods, and verification.
- Always use `MockBehavior.Strict` to catch unexpected calls.
- Wrap each `Mock.Setup` chain in an `Expect_<Call>_On(dependency, ...)` helper method for
  readability and resilience to dependency shape changes.

#### `CreateSut()`

Always add a `CreateSut()` method that constructs the subject under test. When the constructor
gains a new dependency, only `CreateSut()` needs to change.

#### Async / Task patterns

- Use `TaskCompletionSource` to represent a task that remains incomplete (e.g., simulating a
  hanging async operation).
- Assert task state without `await`: `.Should().BeComplete()`, `.Should().NotBeComplete()`,
  `.Should().BeCanceled()`, `.Should().BeFaultedWith(ex)`.
- Do not `await` tasks in unit tests when you intend to verify synchronous completion; assert
  on the Task object directly instead.
- For every `async` method on a dependency, cover all of the following scenarios:
  - Returns completed task → code under test continues
  - Returns incomplete task → code under test awaits (stays incomplete)
  - Incomplete task then completes → code under test resumes
  - `CancellationToken` fires, throws `TaskCanceledException` → returned task `IsCanceled`
  - `CancellationToken` fires, dependency returns complete → returned task `IsCanceled`
  - `CancellationToken` fires, dependency stays incomplete → code stays incomplete
  - Dependency throws directly (no `Task` returned) → exception propagates
  - Dependency returns faulted `Task` → propagates

#### E2E tests (Gherkin)

Prefer the Headless host for new E2E tests — cross-platform, no display required:

```bash
dotnet build src/AdaptiveRemote.Headless/AdaptiveRemote.Headless.csproj
pwsh src/AdaptiveRemote.Headless/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test --filter "FullyQualifiedName~Host.Headless"
```

Write Gherkin scenarios before implementing. Use existing steps whenever possible.

New steps must be generalized, not single-purpose:

- `When` = action a human could perform; `Then` = result a human could verify;
  `Given` = state a human could set up. A failed test should be reproducible manually
  without internal knowledge of the implementation.
- Step definitions: argument/state validation plus a single test service call. No logic in
  step definitions — put it in test service methods.
- Step definitions and test service methods are never `async`. Use
  `WaitHelper.WaitForAsyncTask` to block on async calls.
  - **Exception:** interprocess test services communicate over JSON-RPC and are inherently
    async. Use the provided synchronous extension methods that wrap
    `WaitHelpers.WaitForAsyncTask` with appropriate timeouts.
- All state-verification steps use `WaitHelpers.ExecuteWithRetries` to poll until the
  expected state is observed or a timeout expires. Action steps that may not succeed on the
  first attempt should also use `WaitHelpers.ExecuteWithRetries`. Never write a manual retry
  loop.
- Timeouts unblock a test that will never pass. They are not performance assertions — set
  them long enough that a correct, unloaded system would always succeed.

### Logging

All log messages are defined as `[LoggerMessage]` source-generated methods in
`src/AdaptiveRemote.App/Logging/MessageLogger.cs`. Never call `logger.LogXxx()` directly —
add new methods to `MessageLogger` instead.

Event IDs are organized in ranges by subsystem:

| Range | Subsystem |
|-------|-----------|
| 100–199 | SpeechRecognitionEngine |
| 200–299 | ConversationController |
| 300–399 | SpeechRecognition |
| 400–499 | SpeechSynthesis |
| 500–599 | ListeningController |
| 600–699 | CommandService |
| 700–799 | ApplicationLifecycle |
| 800–899 | TiVoConnection |
| 900–999 | UdpService |
| 1000–1099 | BroadlinkCommandService |
| 1100–1199 | CompiledLayoutService (backend) |
| 1200–1299 | RawLayoutService (backend) |
| 1300–1699 | (reserved — App subsystems: ProgrammaticSettings, ScopedBackgroundProcess, ConversationState, SamplesRecorder, TestEndpointService, CognitoTokenService) |
| 1700–1799 | LayoutProcessingService (backend) |
| 1800–1899 | LayoutCompilerService (backend) |

Assign new log messages the next unused ID in the appropriate range. When replacing an
existing message, use exact text including whitespace, newlines, and punctuation.

In tests, verify log output using `MockLogger.VerifyMessages(log => { log.MethodName(...); })`.

### Async in Application Code

- Fetch async-backed data up front before entering processing-heavy code. Don't scatter
  async calls through processing logic just to retrieve data on demand — fetch first,
  process second.
- Always include a `CancellationToken` parameter in every async API. Never provide a
  default value — callers must explicitly decide to pass `CancellationToken.None`.

### Testable State Design

- For services with significant internal state, extract that state into a Data object or
  ViewModel. The service acts on the object; the object's fields are directly settable and
  readable in tests — no reflection or internal-visible hacks needed.
- Views (Blazor components, WPF controls) must be minimal: only enough to display and
  interact with the ViewModel. All logic goes in a controller service that manipulates the
  ViewModel and handles its change events. Because controller services have no UI
  dependencies, they can be fully unit-tested.

### Project Layout

Before creating new files, read `src/_doc_Projects.md` and the relevant `_doc_Services.md`
to confirm the correct project and folder for each new file.

---

## Code of Conduct

This project follows the
[Contributor Covenant Code of Conduct](https://www.contributor-covenant.org/version/2/1/code_of_conduct/).
By participating, you are expected to uphold this code.

---

Thank you for helping make AdaptiveRemote more accessible and reliable for everyone!

