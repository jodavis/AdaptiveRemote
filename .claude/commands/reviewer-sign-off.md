---
description: Sign-off review for the AdaptiveRemote dev-team pipeline. After the developer has addressed review comments, checks whether each previously requested change has been resolved and scans modified files for new issues. Outputs a JSON result indicating approved or changes_requested.
---

You are performing a sign-off review for work item $WORK_ITEM_ID.

**Task brief:**
$TASK_BRIEF

**PR URL:** $PR_URL

---

## Step 1 — Load guidelines

Read `CONTRIBUTING.md` in full. These are the standards against which you are reviewing.

## Step 2 — Push latest changes to remote

The developer has made fixes since the last review. Ensure the latest commits are visible
on the remote before reviewing. Check `git status` and `git log --oneline -5` to understand
what has changed since the last push.

## Step 3 — Retrieve prior review threads

Using the GitHub MCP, fetch all existing review comments on the PR at `$PR_URL`. For each
unresolved thread, note:
- What was the original issue?
- What file and line was it on?
- Has that file/line been modified since the comment was posted?

## Step 4 — Check each prior thread for resolution

For each unresolved review comment:

1. Read the relevant section of the latest code
2. Determine whether the issue has been adequately addressed:
   - **Resolved:** the problem no longer exists in the code. Resolve the thread via the
     GitHub MCP and note it as resolved in your output summary.
   - **Partially addressed:** the developer made a change but the underlying problem
     remains or a different instance was missed. Add a follow-up comment explaining what
     still needs to be done.
   - **Not addressed:** the code is unchanged. Add a follow-up comment restating what is
     needed and why, more clearly if the original comment was ambiguous.

## Step 5 — Scan modified files for new issues

Identify all files that were modified since the last review push (use the PR diff or git
log). Scan **only those files** for new issues introduced by the developer's fix — do not
re-review unmodified code.

Apply the same priority order as the first-pass review:
1. Requirements, 2. Correctness/fault tolerance, 3. Security, 4. Performance,
5. Documentation, 6. Code style (note only)

Post new inline review comments for any new Priority 1–5 issues found in the modified
files.

## Step 6 — Submit the sign-off review

Use the GitHub MCP to submit a **pull request review** (not a plain PR comment) via
`POST /repos/{owner}/{repo}/pulls/{pull_number}/reviews`. Any new issues should be
inline review comments attached to the specific file and line. Submit with event type:
- **APPROVE** if all prior threads are resolved (or resolved via this pass) and no new
  Priority 1–5 issues were found in the modified files
- **REQUEST_CHANGES** otherwise

## Step 6a — Hand off to human reviewer (approved only)

If the review outcome is **approved**, do the following before writing the output summary:

1. Call `mcp__jira__lookupJiraAccountId` with `$REVIEW_ASSIGNEE_EMAIL` to get the human reviewer's account ID.
2. Assign the Jira issue to that account with `mcp__jira__editJiraIssue`.
3. Call `mcp__github__add_pull_request_review_request` to request a review from `$REVIEW_ASSIGNEE_EMAIL` on `$PR_URL`.
4. Add a brief Jira comment with `mcp__jira__addCommentToJiraIssue`: "PR ready for human review — reviewer requested on GitHub."

## Step 7 — Output

Write a concise summary:
- List each prior comment and whether it was resolved or still needs work
- List any new issues found in the modified files

Then output the JSON result as the final line:

```json
{"status": "approved|changes_requested"}
```
