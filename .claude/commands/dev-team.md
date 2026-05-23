---
description: Entry point for the dev-team agent pipeline. Routes to the correct pipeline based on the work item request, then starts the pipeline script and relays its output.
argument-hint: <request, e.g. "implement ADR-172" or "fix issue #444">
---

## Request

$ARGUMENTS

## Role

You determine which pipeline to run and which research skill to use, then start the
pipeline script and relay its output to the user. The script is the orchestrator — it
drives every phase (research, implementation, build, test, fixes). Once you have started
the script you are a passive observer.

**Never attempt to:**
- Fix build errors or test failures
- Edit source files or test files
- Invoke agent skills directly (researcher-plan, developer-implement, developer-fix, etc.)
- Take any action in response to failures reported in the script output

If the script exits with an error, report the final output to the user and stop. Do not
attempt recovery.

## Steps

### 1 — Determine pipeline and work item ID

Analyze the request using your judgment:

- If the request refers to a **Jira task** — e.g. "implement ADR-123", "ADR-123", or
  any `[A-Z]+-\d+` pattern — use:
  - Pipeline: `implementation-pipeline`
  - Research skill: `researcher-plan`
  - Work item ID: the Jira key as-is (e.g. `ADR-123`)

- If the request refers to a **GitHub issue** — e.g. "fix issue #444", "#444", or
  any `#\d+` pattern — use:
  - Pipeline: `fix-issue-pipeline`
  - Research skill: `researcher-issue`
  - Work item ID: `GH-<number>` (strip the `#`, e.g. `#444` → `GH-444`)

- If the intent is unclear, tell the user:

  > I'm not sure which pipeline to use for this request. Provide a Jira task key
  > (e.g. ADR-123) to use the implementation pipeline, or a GitHub issue number
  > (e.g. #444) to use the fix-issue pipeline.

  Then stop.

### 2 — Check the platform

```bash
python -c "import sys; print(sys.platform)"
```

### 3 — Start the pipeline script in the background

```bash
python -u .claude/scripts/dev_team.py <work-item-id> --workflow .claude/scripts/<pipeline>.md --research-skill <research-skill>
```

### 4 — Stream output

**Immediately** call the Monitor tool on the background process to stream its output.
Do not wait. Do not use TaskOutput. Use the platform-appropriate tail command:
- **`win32`**: `powershell -Command "Get-Content -Wait -Path '<task-output-path>'"`
- **anything else**: `tail -f <task-output-path>`

Stream all output to the user as it arrives until the process exits.

### 5 — Report exit status

When the process exits, report its exit status to the user. Take no further action.
