---
description: Address build errors, test failures, or code review comments against previously implemented work. Reads the original brief and work summary for context, then fixes each issue and returns a prose summary of changes.
argument-hint: <work-item-id>
---

## Inputs

Work item ID: `$ARGUMENTS`

The following context must be embedded in the prompt by the caller:

- **Original task brief** — the full prose brief produced by `researcher-plan`
- **Original work summary** — the structured prose summary produced by `developer-implement`
- **Issues to fix** — a prose list of build errors, test failures, or code review comments

All three are required. If any are missing, stop and tell the caller what is needed.

---

## Steps

### 1 — Load standards

Invoke the `developer-patterns` skill. Read `CLAUDE.md` for project-wide conventions.

### 2 — Understand context

Read the original task brief and work summary to understand what was built and why. Then
read each issue to be fixed.

### 3 — Triage

For each issue:

- **Build error:** locate the root cause in the source or test files; do not patch over
  symptoms.
- **Test failure:** before fixing the production code, confirm whether the test itself is
  correct. If the test is wrong, fix the test and explain why in the report. If the test is
  right, write or verify a failing unit test that isolates the defect, then fix the code.
- **Code review comment:** read the comment, understand the intent, and apply the change.
  If you disagree with the comment, note it in the report and apply the change anyway unless
  it would introduce a correctness problem.

### 4 — Fix each issue

Address issues one at a time. After each fix, build and test the affected code to confirm
the fix works without introducing new failures:

```bash
dotnet build <project-path>
dotnet test <test-project-path>
```

### 5 — Self-review

Review the diff for unintended scope, missed issues, and convention violations.

### 6 — Report

Return a fix summary as structured prose: for each issue, one sentence describing what was
changed and why.
