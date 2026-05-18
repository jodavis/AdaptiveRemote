---
description: Architectural and design patterns for the AdaptiveRemote developer. Read this before writing any application or test code. Covers async design, testable state, E2E test authoring, and project layout — patterns not already in CLAUDE.md.
---

Read CLAUDE.md for project-wide test conventions (naming, AAA structure, Moq setup, CreateSut,
async task patterns, logging, quality gates). The patterns below are supplemental.

---

## Async in application code

- Fetch async-backed data up front before entering processing-heavy code. Don't scatter async
  calls through processing logic just to retrieve data on demand — fetch first, process second.
- Always include a `CancellationToken` parameter in every async API. Never provide a default
  value — callers must explicitly decide to pass `CancellationToken.None`.

## Testable state design

- For services with significant internal state, extract that state into a Data object or
  ViewModel. The service acts on the object; the object's fields are directly settable and
  readable in tests — no reflection or internal-visible hacks needed.
- Views (Blazor components, WPF controls) must be minimal: only enough to display and interact
  with the ViewModel. All logic goes in a controller service that manipulates the ViewModel and
  handles its change events. Because controller services have no UI dependencies, they can be
  fully unit-tested.

## E2E / API tests (Gherkin)

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

## Project layout

Before creating new files, read `src/_doc_Projects.md` and the relevant `_doc_Services.md`
to confirm the correct project and folder for each new file.
