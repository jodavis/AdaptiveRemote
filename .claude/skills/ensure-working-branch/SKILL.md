---
name: ensure-working-branch
description: >
  Ensures the repository is on the correct working branch for a task, creating it from the
  correct base branch if it does not yet exist.
  Use this skill before reading or writing any repository files to confirm the branch is ready.
argument-hint: <work-item-id>
---

Use this skill when:
- You are about to write code or modify files and need to be on the correct working branch

Do NOT use this skill when:
- You already know the working branch is checked out and up to date

## Arguments

- First argument — a **work-item-id** (e.g. `ADR-123`, `Issue-444`). Required.

## Determining work-item-type

Derive `work-item-type` from the `work-item-id` pattern (see `identify-project-work-items`):

| work-item-id pattern | work-item-type |
|---|---|
| `ADR-\d+` (Jira key) | `jira` |
| `Issue-\d+` (GitHub issue) | `github` |

## Steps

### 1 — Compute the working branch name

The working branch is always `dev/claude/<work-item-id>`. Derive it directly from the ID.

### 2 — Determine the base branch

#### 2a — Search the repo for a spec file

Search the repository for `_spec_*.md` files that contain the work-item-id:

```bash
grep -rl "<work-item-id>" . --include="_spec_*.md"
```

If a spec file is found, read it and look for a Jira key (pattern `[A-Z]+-\d+`) that
appears in a heading or field labelled `Epic:`, `Parent:`, or `Epic ID:`. That key is the
Epic ID.

#### 2b — Query Jira if no Epic ID found in spec

If no Epic ID was found in step 2a and `work-item-type` is `jira`:

Call `mcp__jira__getJiraIssue` with the `work-item-id`. Look for a `parent` or `epic`
field on the returned issue and extract its key (e.g. `ADR-200`). That key is the Epic
ID. If the issue has no parent or the parent is not a Jira key, continue with no Epic ID.

#### 2c — Find the epic feature branch

If an Epic ID was found in step 2a or 2b, search remote branches for it:

```bash
git fetch origin
git branch -r | grep "feature/<epic-id>"
```

Strip the `origin/` prefix from the matching branch name (e.g. `feature/ADR-200-infrastructure`). That is the base branch. If more than one branch matches, prefer the one most recently pushed.

#### 2d — Fallback: nearest ancestor feature branch

If no Epic ID was found and step 2c was not reached, check for the nearest ancestor
`feature/*` remote branch:

```bash
git branch -r --merged HEAD | grep "feature/"
```

Use the closest ancestor `feature/*` branch as the base branch.

#### 2e — Fallback or error

If no `feature/*` branch has been found:
- `work-item-type` is `jira`: stop and report an error — a feature branch is required for
  Jira tasks.
- Otherwise: use `main` as the base branch.

### 3 — Prepare the working branch

Fetch the latest state from the remote:

```bash
git fetch origin
```

If the working branch already exists locally or on the remote, check it out and pull:

```bash
git checkout dev/claude/<work-item-id>
git pull origin dev/claude/<work-item-id>
```

If the working branch does not yet exist, create it from the base branch:

```bash
git checkout -b dev/claude/<work-item-id> origin/<base-branch>
```

---

If all steps complete successfully, respond with one word: `successful`

If any step fails, stop and report the failure in detail.
