---
description: Address build errors, test failures, or code review comments against previously implemented work. Reads the original brief and work summary for context, then fixes each issue and returns a prose summary of changes.
argument-hint: <work-item-id>
---

## Inputs

Work item ID: `$ARGUMENTS`

### Original task brief

$TASK_BRIEF

---

### Work summary (all prior implementation and fix rounds)

$WORK_SUMMARIES

---

### Issues to fix

$ISSUES

---

## Steps

### 1 — Load standards

Invoke the `developer-patterns` skill (loads all guidelines from `CONTRIBUTING.md`).
Read `CLAUDE.md` for quality gates and operational conventions.

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

Address issues one at a time. After each fix:

1. Build and test only the project(s) you changed to confirm the fix works without
   introducing new failures:

   ```bash
   dotnet build <project-path>
   dotnet test <test-project-path>
   ```

   **Scope:** Do **not** run `scripts/validate-build` or `scripts/validate-tests`. Those
   are full pipeline validation scripts run by the orchestrator after this step — running
   them here is redundant and slows the fix loop.

2. Commit the fix immediately with a message describing the specific issue resolved:

   ```bash
   git add -A
   git commit -m "$ARGUMENTS: <one-line description of what was fixed and why>"
   ```

   One commit per issue keeps the git history readable and makes individual fixes easy to
   review. Do not batch multiple fixes into a single commit.

Do not push — the pipeline pushes after all fixes pass full validation.

### 5 — Self-review

Review the diff for unintended scope, missed issues, and convention violations.

### 6 — Report

Return a fix summary as structured prose: for each issue, one sentence describing what was
changed and why.
