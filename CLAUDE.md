# AdaptiveRemote

AdaptiveRemote is an accessible remote control for TV/AV equipment built for users with limited or total loss of mobility. Accessibility is the primary design constraint: vision accessibility first, speech recognition second, eye-gaze input third.

> If you are planning, writing, or reviewing code, read the code guidelines in `CONTRIBUTING.md`.

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

See `CONTRIBUTING.md` for logging conventions (`[LoggerMessage]` source-gen, event ID
ranges, test verification with `MockLogger.VerifyMessages`).

## Testing

See `CONTRIBUTING.md` for test naming, structure, mock setup, `CreateSut()`, async
scenario matrix, and E2E (Gherkin/Headless) conventions.

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

1. `scripts/validate-build` — clean build, zero warnings (`dotnet build /warnaserror` is the underlying command but the script also cleans first)
2. `scripts/validate-tests` — all unit and headless E2E tests pass
3. Affected `_doc_*.md` files are updated

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
