# AdaptiveRemote

AdaptiveRemote is an accessible remote control for TV/AV equipment built for users with limited or total loss of mobility. Accessibility is the primary design constraint: vision accessibility first, speech recognition second, eye-gaze input third.

## Read Before Making Changes

Read the `_doc_*.md` file for any area you plan to modify:

| Area | File |
|------|------|
| Project boundaries | `src/_doc_Projects.md` |
| Services architecture | `src/AdaptiveRemote.App/Services/_doc_Services.md` |
| Lifecycle subsystem | `src/AdaptiveRemote.App/Services/Lifecycle/_doc_Lifecycle.md` |
| Commands subsystem | `src/AdaptiveRemote.App/Services/Commands/_doc_Commands.md` |
| Broadlink / IR | `src/AdaptiveRemote.App/Services/Broadlink/_doc_Broadlink.md` |
| Speech / Conversation | `src/AdaptiveRemote.App/Services/Conversation/_doc_Conversation.md` |
| MVVM | `src/AdaptiveRemote.App/Mvvm/_doc_Mvvm.md` |
| UI components | `src/AdaptiveRemote.App/Components/_doc_UI.md` |
| E2E test architecture | `test/_doc_EndToEndTests.md` |
| Simulated devices | `test/AdaptiveRemote.EndToEndTests.TestServices/_doc_SimulatedDevices.md` |
| Backend services | `src/_doc_BackendDevelopment.md` |

## Tech Stack

- **.NET 10 / C#** — Windows-only (`net10.0-windows`), nullable reference types enabled
- **UI:** Blazor WebView in WPF (primary), Playwright headless (CI/E2E)
- **Unit tests:** MSTest + Moq + FluentAssertions
- **E2E tests:** Reqnroll (BDD/Gherkin) with multiple host variants; prefer Headless for new tests
- **Build:** `dotnet` CLI; warnings are treated as errors (`/warnaserror`)

## Logging

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

Assign new log messages the next unused ID in the appropriate range. When replacing an existing
message, use exact text including whitespace, newlines, and punctuation.

In tests, verify log output using `MockLogger.VerifyMessages(log => { log.MethodName(...); })`.

## Testing

### Naming
`ClassName_Method_Scenario_ExpectedResult`
Example: `TiVoService_InitializeAsync_WaitsForTiVoLocator`

### Structure
Use AAA (Arrange-Act-Assert). Use `[TestInitialize]` for mock setup and `[TestCleanup]` for
mock verification. Group setup calls into `Expect_*` helper methods.

### Async / Task patterns
- Use `TaskCompletionSource` to represent a task that remains incomplete (e.g., simulating a
  hanging async operation).
- Assert task state without `await`: `.Should().BeComplete()`, `.Should().NotBeComplete()`,
  `.Should().BeCanceled()`, `.Should().BeFaultedWith(ex)`.
- Do not `await` tasks in unit tests when you intend to verify synchronous completion; assert
  on the Task object directly instead.

### E2E tests
Prefer the Headless host for new E2E tests — cross-platform, no display required:

```bash
dotnet build src/AdaptiveRemote.Headless/AdaptiveRemote.Headless.csproj
pwsh src/AdaptiveRemote.Headless/bin/Debug/net10.0/playwright.ps1 install chromium  # if tests crash at startup with a JSON-RPC disconnect
dotnet test --filter "FullyQualifiedName~Host.Headless"
```

## Documentation

### `_spec_*.md` — pre-implementation design docs

Before writing code for a new subsystem or significant feature, create a `_spec_*.md` file
next to where the code will live. Use `/spec <feature description>` to draft one.

- Include planned implementation detail (interfaces, classes, data flow) — source doesn't
  exist yet, so the spec is the reference
- Mark unresolved items with `> TBD: reason` and list them in an Open Questions section
- Once implementation is complete, replace the spec with a `_doc_*.md` file (drop
  implementation detail; link to source instead)

### `_doc_*.md` — living architecture docs

`_doc_*.md` files live next to the code they describe. When you add a new subsystem or
significantly change a design, create or update the relevant `_doc_*.md`:

- Focus on design intent and non-obvious decisions, not implementation details
- Link to source files rather than duplicating code in docs
- Keep language clear and structured for future agents and contributors

## Quality Gates

A change is not complete until all of the following pass:

1. `dotnet build /warnaserror` — zero warnings
2. `dotnet test` — all unit tests pass
3. Headless E2E tests pass
4. Affected `_doc_*.md` files are updated

## Accessibility

Never introduce accessibility regressions. Priority order:
1. Vision accessibility (highest)
2. Speech recognition
3. Eye-gaze input
4. Keyboard/mouse (no regressions, but lowest priority)

## Workflow

- **Branch naming:** `dev/jodavis/ADR-###-short-description`
- **Before committing:** Self-review the diff as if it were a code review
- **Commit messages:** Clear and descriptive; explain what and why

## Communication

- Do not congratulate or add unnecessary praise — focus on substance
- Engage critically; surface issues and question assumptions
- When your answer is not fully certain, include a confidence score (0–100)
- If confidence is below 90, explain the source of uncertainty
- Never present uncertain information as fact
