---
description: Sign-off review for the AdaptiveRemote dev-team pipeline. After the developer has addressed review comments, checks whether each previously requested change has been resolved and scans modified files for new issues. Returns a structured JSON result — the pipeline's top-level session handles all GitHub API calls (thread resolution, PR ready, reviewer assignment).
---

You are performing a sign-off review for work item $WORK_ITEM_ID.

**Task brief:**
$TASK_BRIEF

**PR URL:** $PR_URL

**Review notes (JSON from first-pass review):**
```json
$REVIEW_NOTES
```

---

## Step 1 — Load guidelines

Read `CONTRIBUTING.md` in full. These are the standards against which you are reviewing.

## Step 2 — Note recent changes

Check `git log --oneline -5` to understand what has changed since the previous review pass.

## Step 3 — Analyse prior review findings

The JSON above contains the first-pass review findings. Parse the `comments` array — each
entry has `path`, `line`, and `body`. These are the issues the developer was asked to fix.

## Step 4 — Check each unresolved thread

For each comment in the `comments` array, read the relevant section of the latest code
and determine:

- **Addressed** — the problem no longer exists.
- **Not addressed** — still present.

## Step 5 — Scan modified files for new issues

Identify all files modified since the last review push (`git diff origin/<base>..HEAD` or
`git log`). Scan **only those files** for new Priority 1–4 issues introduced by the fix:

1. Correctness/fault tolerance, 2. Security, 3. Performance, 4. Documentation

Note any new issues in the sign-off body. Style issues (Priority 5) are noted only.

## Step 6 — Write the sign-off body

Write a concise plain-text summary:
- List each prior comment and whether it was addressed or still needs work.
- List any new Priority 1–4 issues found in the modified files.

This text is posted verbatim to the GitHub PR by the pipeline's top-level session.

## Step 7 — Output

Output the final JSON as the last line. This is the machine-readable result the pipeline
uses to drive GitHub API calls and the sign-off decision.

```json
{
  "sign_off_body": "<full text from step 6>",
  "status": "approved|changes_requested"
}
```

Set `"approved"` only if **all** of the following are true:
- All prior comments have been addressed
- No new Priority 1–4 issues were found in modified files

Set `"changes_requested"` if any comments remain unaddressed or new blocking issues were
found.
