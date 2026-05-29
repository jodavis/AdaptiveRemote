---
description: First-pass code review for the AdaptiveRemote dev-team pipeline. Retrieves the PR diff via git, reviews all changes against requirements and quality criteria, and returns a structured JSON review. The pipeline's top-level session posts it to GitHub. Outputs a JSON result indicating approved or changes_requested.
---

You are performing the first-pass code review for work item $WORK_ITEM_ID.

**Task brief:**
$TASK_BRIEF

**Base branch:** $BASE_BRANCH

**Spec file path:** $SPEC_PATH

---

## Step 1 — Load guidelines

Read `CONTRIBUTING.md` in full. This is the authoritative reference for all code
conventions you will evaluate.

## Step 2 — Understand the requirements

Re-read the task brief above and extract the exit criteria — the explicit list of things
the implementation must do or must not do. You will check each one during review.

Read the relevant `_doc_*.md` architecture files for any subsystem touched by this change.
Use `grep -rl "^Summary:" src test --include="_doc_*.md"` to discover available docs, then
read the `Summary:` line of each match to find the relevant ones.

## Step 3 — Retrieve and read the diff

Fetch the diff using git (no GitHub API needed):

```bash
git diff origin/$BASE_BRANCH..HEAD
```

Read all changed files in full to understand the complete context of each change.

## Step 4 — Review the changes

Evaluate the diff against each dimension below, in priority order. For each issue you
find, note the file, line number, and a clear description of the problem.

### Priority 1 — Correctness and fault tolerance

- Are all exception paths handled? No swallowed exceptions, no empty `catch` blocks (unless there's a comment with a good justification).
- Are `CancellationToken` parameters present in every async method signature? No default
  values — callers must pass explicitly.
- Are there blocking calls (`.Result`, `.Wait()`, `Thread.Sleep`) on async code paths?
- Does error handling propagate faithfully, or does it silently discard failures?

### Priority 2 — Security

- Is user input validated at system boundaries?
- Are there SQL injection, command injection, or path traversal risks?
- Is sensitive data (tokens, passwords, PII) logged or returned in error messages?
- Are authentication/authorization checks present where the architecture requires them?

### Priority 3 — Performance

- Are there N+1 query patterns (fetching inside a loop that could be batched)?
- Is there synchronous I/O on async code paths?
- Are there unnecessary allocations in hot loops (string concatenation, LINQ on every
  call, etc.)?
- Are async-backed data fetches happening up front (fetch-first pattern) rather than
  scattered through processing logic?

### Priority 4 — Documentation

- Does new code conform to the design described in the relevant `_doc_*.md` files?
- If the implementation changed the design (new interface, changed responsibility,
  new dependency), has the relevant `_doc_*.md` been updated?
- Have new `_doc_*.md` files been added where necessary?

### Priority 5 — Code style (note, do not block)

- Do naming conventions follow CONTRIBUTING.md (`ClassName_Method_Scenario_ExpectedResult`
  for tests, etc.)?
- Do log messages use `[LoggerMessage]` source-generated methods?
- Do tests use `MockBehavior.Strict` and `Expect_*` helpers?
- Is there a `CreateSut()` method?

## Step 5 — Format the review

Write a concise plain-text summary of all issues found (one bullet per issue, Priority
1–4 first, style issues last). For each file/line issue, add it to the `comments` array
in the output JSON — the scrum master will post it as an inline thread comment on the PR.

## Step 6 — Output

Output the review body, inline comments, and status as the final JSON line:

```json
{"body": "<overall summary for the review body>", "comments": [{"path": "relative/file.py", "line": 42, "body": "Issue description"}, ...], "status": "approved|changes_requested"}
```

The `body` is the overall review summary. The `comments` array contains one entry per
inline issue, with `path` (relative file path), `line` (the line number), and `body`
(the specific issue description). Omit `comments` or use an empty array if there are no
inline issues. The scrum master will post this as a real GitHub PR review with inline
thread comments.

Use `"approved"` if no Priority 1–4 issues were found; `"changes_requested"` otherwise.
