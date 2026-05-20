---
description: Entry point for the dev-team agent pipeline. Runs the Researcher phase for a Jira work item.
argument-hint: <Jira task key, e.g. ADR-172>
---

## Work item

$ARGUMENTS

## Role

Your only job is to start the pipeline script and relay its output to the user. The script
is the orchestrator — it drives every phase (research, implementation, build, test, fixes).
You are a passive observer.

**Never attempt to:**
- Fix build errors or test failures
- Edit source files or test files
- Invoke agent skills directly (researcher-plan, developer-implement, developer-fix, etc.)
- Take any action in response to failures reported in the script output

If the script exits with an error, report the final output to the user and stop. Do not
attempt recovery.

## Steps

1. Check the platform:

```bash
python -c "import sys; print(sys.platform)"
```

2. Start the pipeline script in the background:

```bash
python -u .claude/scripts/dev_team.py $ARGUMENTS --workflow .claude/scripts/implementation-pipeline.md
```

3. **Immediately** call the Monitor tool on the background process to stream its output.
   Do not wait. Do not use TaskOutput. Use the platform-appropriate tail command:
   - **`win32`**: `powershell -Command "Get-Content -Wait -Path '<task-output-path>'"`
   - **anything else**: `tail -f <task-output-path>`

   Stream all output to the user as it arrives until the process exits.

4. When the process exits, report its exit status to the user. Take no further action.
