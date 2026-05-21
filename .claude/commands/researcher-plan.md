---
description: Produce a concise task brief for a Jira task, synthesized from its spec file section and the relevant architecture docs and source code
argument-hint: <Jira task key> <path to spec file>
---

## Inputs

Task key: the first token of `$ARGUMENTS`  
Spec file: the second token of `$ARGUMENTS`

Both are required. If either is missing, stop and tell the caller:

> Usage: `/researcher-plan <task-key> <path/to/_spec_Feature.md>`

---

## Steps

### 1 — Find the task section

Read the spec file. Locate the section that references the task key. This is typically
a header or checkbox list item that includes the key (e.g., `ADR-123`).

If no section is found for the task key, stop and tell the caller:

> Task key `<key>` was not found in `<spec file>`. Verify the key and spec path are correct.

### 2 — Read architecture docs

Identify which subsystems and areas this task touches based on the spec section and its
surrounding design decisions. Read the relevant `_doc_*.md` files for those areas.

Always read `src/_doc_Projects.md`. Read additional docs as needed:

| Area | File |
|------|------|
| Services architecture | `src/AdaptiveRemote.App/Services/_doc_Services.md` |
| Lifecycle | `src/AdaptiveRemote.App/Services/Lifecycle/_doc_Lifecycle.md` |
| Commands | `src/AdaptiveRemote.App/Services/Commands/_doc_Commands.md` |
| Broadlink / IR | `src/AdaptiveRemote.App/Services/Broadlink/_doc_Broadlink.md` |
| Conversation | `src/AdaptiveRemote.App/Services/Conversation/_doc_Conversation.md` |
| MVVM | `src/AdaptiveRemote.App/Mvvm/_doc_Mvvm.md` |
| UI components | `src/AdaptiveRemote.App/Components/_doc_UI.md` |
| E2E tests | `test/_doc_EndToEndTests.md` |
| Simulated devices | `test/AdaptiveRemote.EndToEndTests.TestServices/_doc_SimulatedDevices.md` |
| Backend | `src/_doc_BackendDevelopment.md` |

### 3 — Read relevant source

Read the source files and interfaces the task will create or modify. Use `Glob` and `Grep`
to locate them. Focus on: existing interfaces the task must implement or call, utilities
to reuse, patterns established in adjacent code.

### 4 — Research external best practices (if needed)

If the task touches a framework or pattern not covered by local docs, use `WebSearch`,
`WebFetch`, or Microsoft Learn to look up best practices. Common areas: .NET DI patterns,
Blazor component lifecycle, MAUI platform-specific code, ASP.NET minimal API conventions.

### 5 — Write the task brief

Return the brief as structured prose to the caller. Do not write it to a file.

The brief must cover:

**Task title and description**  
One sentence stating what the task accomplishes and why.

**Exit criteria**  
Copy the exit criteria checklist from the spec section verbatim. If the spec uses Gherkin
scenarios, include them.

**Key design decisions**  
Decisions already made in the spec that directly constrain this task's implementation.
Do not include decisions from other parts of the spec that don't affect this task.

**Files and interfaces to create or modify**  
List each file by path. For each: whether it is new or modified, and one sentence on
what changes. Call out existing utilities, base classes, or patterns the Developer should
reuse rather than reinvent — include file paths.

**Known ambiguities**  
Concrete questions the Developer may need answered before or during implementation.
Only include genuine gaps — not rhetorical questions or things already resolved in the spec.
