---
description: Update a Jira work item when the dev-team pipeline has exhausted all fix retries. Assigns the issue to the human reviewer and adds a failure comment with log paths.
argument-hint: <work-item-id>
---

## Inputs

Work item ID: `$ARGUMENTS`

Human reviewer email: `$REVIEW_ASSIGNEE_EMAIL`

Log paths:

$LOG_PATHS

---

## Steps

### 1 — Assign to human reviewer

1. Call `mcp__jira__lookupJiraAccountId` with `$REVIEW_ASSIGNEE_EMAIL` to get the account ID.
2. Assign the Jira issue to that account with `mcp__jira__editJiraIssue`.

### 2 — Add failure comment

Call `mcp__jira__addCommentToJiraIssue` on `$ARGUMENTS` with:

> Pipeline failed after max retries — manual intervention needed.
>
> $LOG_PATHS
