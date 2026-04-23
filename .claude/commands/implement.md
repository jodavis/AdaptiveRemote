---
description: Implement a Jira task end-to-end: branch, implement from spec, build/test, code review, commit, PR, and iterative review cycle
argument-hint: <Jira task key, e.g. ADR-123>
---

## Task to implement

$ARGUMENTS

## Available spec files

!`find . -name "_spec_*.md" -not -path "./.git/*" | sort`

## Available architecture docs

!`find . -name "_doc_*.md" -not -path "./.git/*" | sort`

## Workflow

Follow the phases below in order. Each phase has explicit pause points — do not skip ahead without user confirmation.

---

### Phase 0 — Plan mode check

Check whether plan mode is currently active in your context.

**If plan mode IS active:** Phases 0–4 are read-only planning steps. Complete them, then call
`ExitPlanMode` to present the implementation plan for user approval. Proceed with Phases 5–11
only after the user approves.

**If plan mode is NOT active:** Tell the user:

> Plan mode is not active. This skill is designed to be invoked in plan mode so you can review
> the implementation plan before any code is written. Do you want to continue without plan mode?

**PAUSE — wait for the user's answer before continuing.**

---

### Phase 1 — Find the spec file

Search the repository for a `_spec_*.md` file that references the task key:

```
grep -rl "TASK_KEY" . --include="_spec_*.md" --exclude-dir=.git
```

(substitute the actual task key from `$ARGUMENTS`)

**If no file is found:** Stop and tell the user:

> No `_spec_*.md` file was found containing `TASK_KEY`. Check that the task key is correct
> and that a spec file exists for this task before continuing.

**PAUSE — do not fall back to keyword search.** A missing spec almost certainly means the
task key or working branch is wrong.

**If a file is found:** Read it fully before continuing.

---

### Phase 2 — Read architecture docs

Read the `_doc_*.md` files relevant to the areas the spec touches. At minimum read
`src/_doc_Projects.md`. Read any others that apply to the feature area.

---

### Phase 3 — Create or switch to branch

Derive a branch slug (5–6 words, kebab-case) from the spec filename or the spec's title.

Branch name format: `dev/claude/TASK_KEY-slug`
Example: `dev/claude/ADR-123-programmable-commands`

Check whether the branch already exists:

```
git branch --list "dev/claude/TASK_KEY*"
```

- **If it exists:** switch to it with `git checkout`.
- **If it does not exist:** create it from the latest `main`:
  ```
  git fetch origin main
  git checkout -b dev/claude/TASK_KEY-slug origin/main
  ```

**Optional Jira status update:** Attempt to set the Jira issue status to "In Progress" using
`mcp__jira__editJiraIssue`. Skip silently if the tool is unavailable or the transition fails.

---

### Phase 4 — Plan the implementation

Produce a concrete implementation plan:

1. List every requirement from the spec that needs new or changed code.
2. Identify existing utilities, interfaces, and patterns to reuse — prefer reuse over
   writing new code.
3. List each file to be created or modified, with a brief description of the change.
4. Note any test coverage needed: unit tests for new logic, E2E scenarios for user-visible
   behaviour.

If the spec contains gaps that would require guessing to implement (missing decisions,
unspecified error cases, unclear interfaces), list them all and ask the user to resolve them.

**PAUSE if you have questions — wait for the user's answers before proceeding.**

If plan mode is active, call `ExitPlanMode` here to present the plan for approval.

---

### Phase 5 — Implement

Follow the plan and spec. Apply conventions from `CLAUDE.md`:

- Log messages must be defined as `[LoggerMessage]` source-generated methods in
  `MessageLogger.cs`. Never call `logger.LogXxx()` directly.
- Assign new log message IDs from the next unused ID in the appropriate subsystem range.
- Never introduce accessibility regressions (priority: vision > speech > eye-gaze > keyboard).
- Update any affected `_doc_*.md` files.

---

### Phase 6 — Quality check (first pass)

Run all Linux-compatible quality gates. The following projects target Windows and cannot run
on Linux — do not attempt them: `Speech.Tests`, `Host.Wpf`, `Host.Console`.

```
dotnet build /warnaserror
dotnet test test/AdaptiveRemote.App.Tests/AdaptiveRemote.App.Tests.csproj
dotnet test test/AdaptiveRemote.EndToEndTests.Host.Headless/AdaptiveRemote.EndToEndTests.Host.Headless.csproj
```

Fix every warning, error, and test failure before continuing. Repeat until all three
commands pass cleanly.

---

### Phase 7 — Code review

Invoke `/simplify` to review the changed code for reuse, quality, and efficiency. Address
all findings. Then invoke `/security-review` to check for security issues. Address all
findings.

After each fix, verify the build and Headless tests still pass.

---

### Phase 8 — Quality check (second pass)

Re-run all three commands from Phase 6. All must pass before continuing.

---

### Phase 9 — Commit and push

Self-review the diff before committing — does every change belong? Is anything missing?

Commit message format: `<type>: <description> [TASK_KEY]`
Example: `feat: add programmable command scheduling [ADR-123]`

Include a brief "why" in the commit body if the change is non-trivial.

Push the branch:

```
git push -u origin dev/claude/TASK_KEY-slug
```

---

### Phase 10 — Create PR and request reviews

Create the pull request using `mcp__github__create_pull_request`:

- **Title:** `[TASK_KEY] <concise feature description>`
- **Body:**
  - Link to the Jira task: `https://jodasoft.atlassian.net/browse/TASK_KEY`
  - 3–5 bullet summary of what changed and why
  - Test plan: what to verify manually

Then:

1. Request Copilot review: `mcp__github__request_copilot_review`
2. Subscribe to PR activity: `mcp__github__subscribe_pr_activity`
3. **Optional Jira status update:** Attempt `mcp__jira__editJiraIssue` to set status to
   "In Review". Skip silently if unavailable.

Tell the user:

> PR created at `<url>`. I've requested a Copilot review. Please review when ready —
> I'll watch for comments.

**PAUSE — wait for review activity (`<github-webhook-activity>` events) before continuing.**

---

### Phase 11 — Review cycle

When review activity arrives:

1. Read all comments carefully.
2. Address every comment:
   - Implement the requested change, OR
   - Explain clearly why the comment doesn't apply or is incorrect — but only when
     genuinely wrong, not to avoid work.
3. Re-run all three quality gate commands from Phase 6.
4. Commit and push the fixes with a clear message referencing the review round.
5. Reply to each addressed comment confirming what was done.

Repeat Phase 11 until the PR is approved or the user explicitly signs off.
