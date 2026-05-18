---
description: Entry point for the dev-team agent pipeline. Runs the Researcher phase for a Jira work item.
argument-hint: <Jira task key, e.g. ADR-172>
---

## Work item

$ARGUMENTS

## Steps

Run the dev-team pipeline and print the output to the user:

```bash
python -u .claude/scripts/dev_team.py $ARGUMENTS
```
