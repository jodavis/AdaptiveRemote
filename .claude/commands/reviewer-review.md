---
description: First-pass code review for the AdaptiveRemote dev-team pipeline. Creates a GitHub PR if one does not exist, retrieves the PR diff, reviews all changes against requirements and quality criteria, and posts a GitHub PR review. Outputs a JSON result indicating approved or changes_requested.
---

You are performing the first-pass code review for work item $WORK_ITEM_ID.

**Task brief:**
$TASK_BRIEF

**PR URL (empty = not yet created):** $PR_URL

**Spec file path:** $SPEC_PATH

---

## Step 1 — Load guidelines

Read `CONTRIBUTING.md` in full. This is the authoritative reference for all code
conventions you will evaluate.

## Step 2 — Understand the requirements

Re-read the task brief above and extract the exit criteria — the explicit list of things
the implementation must do or must not do. You will check each one during review.

Read the relevant `_doc_*.md` architecture files for any subsystem touched by this change.
Use the area→file table in `CLAUDE.md` to find them.

## Step 3 — Create the PR if needed

If `$PR_URL` is empty:

1. Determine the current branch name (`git branch --show-current` or read `.git/HEAD`)
2. Use the GitHub MCP to create a pull request with:
   - A clear, descriptive title (summarises what the work item implements)
   - A body that includes: the work item ID, a summary of what changed, and any notable
     design decisions from the implementation
3. Record the PR URL — you will include it in your output JSON

If `$PR_URL` is already set, use that PR for the review.

## Step 4 — Retrieve and read the diff

Use the GitHub MCP to fetch the PR diff. Read all changed files in full to understand the
complete context of each change.

## Step 5 — Review the changes

Evaluate the diff against each dimension below, in priority order. For each issue you
find, note the file, line number, and a clear description of the problem.

### Priority 1 — Requirements

Check each exit criterion from the task brief:
- Is it implemented?
- Is it implemented correctly (not just partially)?
- Are there edge cases the brief implied but the implementation misses?

### Priority 2 — Correctness and fault tolerance

- Are all exception paths handled? No swallowed exceptions, no empty `catch` blocks.
- Are `CancellationToken` parameters present in every async method signature? No default
  values — callers must pass explicitly.
- Are there blocking calls (`.Result`, `.Wait()`, `Thread.Sleep`) on async code paths?
- Does error handling propagate faithfully, or does it silently discard failures?

### Priority 3 — Security

- Is user input validated at system boundaries?
- Are there SQL injection, command injection, or path traversal risks?
- Is sensitive data (tokens, passwords, PII) logged or returned in error messages?
- Are authentication/authorization checks present where the architecture requires them?

### Priority 4 — Performance

- Are there N+1 query patterns (fetching inside a loop that could be batched)?
- Is there synchronous I/O on async code paths?
- Are there unnecessary allocations in hot loops (string concatenation, LINQ on every
  call, etc.)?
- Are async-backed data fetches happening up front (fetch-first pattern) rather than
  scattered through processing logic?

### Priority 5 — Documentation

- Does new code conform to the design described in the relevant `_doc_*.md` files?
- If the implementation changed the design (new interface, changed responsibility,
  new dependency), has the relevant `_doc_*.md` been updated?

### Priority 6 — Code style (note, do not block)

- Do naming conventions follow CONTRIBUTING.md (`ClassName_Method_Scenario_ExpectedResult`
  for tests, etc.)?
- Do log messages use `[LoggerMessage]` source-generated methods?
- Do tests use `MockBehavior.Strict` and `Expect_*` helpers?
- Is there a `CreateSut()` method?

## Step 6 — Post the GitHub PR review

Using the GitHub MCP, submit a single PR review:
- **APPROVE** if there are no Priority 1–5 blocking issues
- **REQUEST_CHANGES** if any Priority 1–5 issue was found

For each issue, post an inline review comment at the relevant file and line. Write comments
that explain what the problem is and why it matters — not just what to change.

Style issues (Priority 6) may be included as inline comments but must be clearly marked as
non-blocking (e.g., "nit:").

## Step 7 — Output

Write a concise plain-text summary of all issues found (one bullet per issue, Priority
1–5 first, style issues last). This summary will be passed to the developer so they can
address each point without re-reading the full PR thread.

Then output the JSON result as the final line:

```json
{"status": "approved|changes_requested", "pr_url": "https://github.com/..."}
```

Use `"approved"` if no Priority 1–5 issues were found; `"changes_requested"` otherwise.
Always include the PR URL even when approving.
