---
description: Entry point for the dev-team agent pipeline. Runs the Researcher phase for a Jira work item.
argument-hint: <Jira task key, e.g. ADR-172>
---

## Work item

$ARGUMENTS

## Steps

1. Check the platform:

```bash
python -c "import sys; print(sys.platform)"
```

2. Start the pipeline script in the background:

```bash
python -u .claude/scripts/dev_team.py $ARGUMENTS
```

3. **Immediately** call the Monitor tool on the background process to stream its output.
   Do not wait. Do not use TaskOutput. Use the platform-appropriate tail command:
   - **`win32`**: `powershell -Command "Get-Content -Wait -Path '<task-output-path>'"`
   - **anything else**: `tail -f <task-output-path>`

   Stream all output to the user as it arrives until the process exits.
