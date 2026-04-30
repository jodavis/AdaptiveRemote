---
description: Implement a Jira task autonomously using a team of specialized sub-agents (Researcher, Developer, Tester, Reviewer) with build, test, and review cycles
argument-hint: <Jira task key, e.g. ADR-161>
---

## Task to implement

Jira Task: $ARGUMENTS

## Available spec files

!`find . -name "_spec_*.md" -not -path "./.git/*" | sort`

## Your role

You are the **orchestrating agent**. You coordinate four specialized sub-agents to implement
this task from spec to merged PR. Keep your own context lean:

- Accept only minimal, structured outputs from sub-agents
- Direct sub-agents to read from and write to GitHub PR and Jira directly — do not relay
  large payloads through yourself

**Before starting**, capture the current working branch — this is the **base branch**
(PR target, Developer branches off this):

```
git branch --show-current
```

---

## Structured output schemas

Two agents return JSON. Validate their output matches before routing.

**Tester output** (array, empty = all pass):
```json
[
  {
    "test": "ClassName_Method_Scenario",
    "failure_type": "assertion | exception | timeout | other",
    "root_cause": "one-sentence diagnosis"
  }
]
```

**Reviewer output** (array, empty = clean):
```json
[
  {
    "file": "relative/path/to/File.cs",
    "line": 42,
    "comment": "description of the issue"
  }
]
```

---

## Workflow

Follow the phases in order. The skill runs autonomously except at the three explicit
**PAUSE** points: spec not found, Developer stuck on ambiguity, or repeated identical
test failures.

---

### Phase 1 — Researcher (Haiku)

Spawn a Researcher agent with `model: haiku`. Pass it:

- The task key (`$ARGUMENTS`)
- The list of spec file paths from the "Available spec files" section above

Researcher instructions:

> You are the Researcher for a software implementation team. Produce a concise task brief
> for the Developer — focused only on what is needed for this specific task, not the whole spec.
>
> **Task key:** TASK_KEY
>
> **Spec files:**
> (paste spec file paths)
>
> **Steps:**
> 1. Grep each spec file for TASK_KEY. If no file matches, output exactly:
>    `{ "status": "not_found" }` and stop.
> 2. Read the matching spec file in full. Find the section for TASK_KEY.
> 3. Read all `_doc_*.md` files relevant to the areas this task touches. At minimum read
>    `src/_doc_Projects.md`.
> 4. Read relevant source files in the areas the task will modify.
> 5. Write a **task brief** as structured prose covering:
>    - Task title and one-sentence description
>    - Exit criteria (checklist)
>    - Key design decisions for this task
>    - Files and interfaces to create or modify; existing patterns and utilities to reuse
>    - Conventions: all log messages via `[LoggerMessage]` source-gen in `MessageLogger.cs`;
>      never call `logger.LogXxx()` directly; no accessibility regressions (priority: vision >
>      speech > eye-gaze > keyboard); follow `.editorconfig` formatting
>    - Known ambiguities — questions the Developer may need answered
>
> Do not return the full spec or doc content — only the information needed for this task.
> Return the task brief as plain prose. The spec file contents, docs, and source files stay
> inside you; do not include them in your output.

**If Researcher returns `{ "status": "not_found" }`**, tell the user:

> No `_spec_*.md` file was found containing `TASK_KEY`. Check that the task key is correct
> and that you are on the right branch before continuing.

**PAUSE — wait for the user before continuing.**

---

### Phase 2 — Developer, first pass (Sonnet)

Spawn a Developer agent with `model: claude-sonnet-4-6`. Pass it:

- The task brief (full text from Researcher)
- Task key, base branch

Developer instructions:

> You are the Developer. Implement the task described in the task brief below.
>
> **Task key:** TASK_KEY
> **Base branch:** BASE_BRANCH
>
> **Task brief:**
> TASK_BRIEF
>
> ---
>
> **Step 1 — Branch setup**
>
> Derive a 5–6 word kebab-case slug from the task title.
> Branch name: `dev/claude/TASK_KEY-slug`
>
> Check if it exists: `git branch --list "dev/claude/TASK_KEY*"`
> - If yes: `git checkout` to it.
> - If no: `git checkout -b dev/claude/TASK_KEY-slug BASE_BRANCH`
>
> Optional (silent fail): set Jira status to "In Progress" via `mcp__jira__editJiraIssue`.
>
> **Step 2 — Clarification check**
>
> Do not read the spec file directly — the task brief is your complete source of truth for
> requirements. If something is missing, use the clarification signal below; do not go to
> the spec yourself.
>
> If the task brief contains ambiguities you cannot resolve by reading the existing code,
> return exactly:
> ```json
> { "status": "needs_clarification", "questions": ["question 1", "question 2"] }
> ```
> and stop. Otherwise continue to Step 3.
>
> **Step 3 — Implement**
>
> Follow all CLAUDE.md conventions:
> - All log messages via `[LoggerMessage]` source-gen methods in `MessageLogger.cs`.
>   Never call `logger.LogXxx()` directly.
> - Assign new log message IDs from the next unused ID in the appropriate subsystem range.
> - No accessibility regressions (priority: vision > speech > eye-gaze > keyboard).
> - Follow `.editorconfig` formatting conventions — write code correctly first rather than
>   relying on the build to catch it.
> - Update any affected `_doc_*.md` files.
>
> **Step 4 — Build**
>
> Do not run tests — that is the Tester agent's responsibility. Only run the build:
>
> ```
> scripts/validate-build.sh
> ```
>
> The script stages new files and cleans before building — do not run `git add -A`
> separately. Fix all warnings and errors and re-run until the build is clean.
>
> **Step 5 — Commit and push**
>
> Commit format: `feat: description [TASK_KEY]`
> Include a brief "why" in the commit body if the change is non-trivial.
>
> ```
> git push -u origin BRANCH_NAME
> ```
>
> Return: `{ "status": "done", "branch": "dev/claude/TASK_KEY-slug" }`

**If Developer returns `{ "status": "needs_clarification" }`:**

Re-spawn the Researcher (Haiku) with the original task brief plus the questions. Researcher
re-reads source and spec to answer. If it cannot answer definitively, it should return:
`{ "status": "cannot_answer", "questions": [...] }`.

If Researcher cannot answer: **PAUSE — ask the user the outstanding questions.**

Once answers are received, re-spawn Developer with the task brief plus the answers appended.

---

### Phase 3 — Tester (Haiku)

Spawn a Tester agent with `model: haiku`. Pass it the branch name.

Tester instructions:

> You are the Tester. Run the full test suite on the given branch and report any failures.
>
> **Branch:** BRANCH_NAME
>
> ```
> git checkout BRANCH_NAME
> scripts/validate-tests.sh
> ```
>
> For each failing test, investigate the root cause by reading the relevant test file and
> the source it tests. Do not just surface the error message — explain *why* the test fails.
> You may re-run individual tests to gather more information.
>
> Return a JSON array matching this schema exactly. Return only the JSON — no other text.
> An empty array means all tests pass.
>
> ```json
> [
>   {
>     "test": "ClassName_Method_Scenario",
>     "failure_type": "assertion | exception | timeout | other",
>     "root_cause": "one-sentence diagnosis"
>   }
> ]
> ```

**Track failures across Tester runs.** If the identical set of failing tests appears in
3 consecutive runs without any change, the Developer is stuck:

**PAUSE — report the repeated failures to the user and wait for guidance.**

**If failures:** re-spawn Developer (Sonnet) with:

> You are the Developer. Fix the failing tests described below.
>
> **Task key:** TASK_KEY
> **Branch:** BRANCH_NAME
> **Task brief:** TASK_BRIEF
>
> **Failing tests:**
> FAILURE_JSON
>
> Check out the branch and fix each failure. You may re-run individual tests to
> verify a specific fix (e.g. `dotnet test --filter "FullyQualifiedName~TEST_NAME"`),
> but do not run the full suite — that is the Tester agent's job. When done, confirm
> the build is still clean, then commit and push:
>
> ```
> git checkout BRANCH_NAME
> scripts/validate-build.sh
> git commit -m "fix: address test failures [TASK_KEY]"
> git push
> ```
>
> Return: `{ "status": "done", "branch": "BRANCH_NAME" }`

Re-run Tester. Repeat until no failures.

**If no failures:** proceed to Phase 4.

---

### Phase 4 — Create PR

Create the pull request. Substitute actual values for all placeholders:

```
gh pr create \
  --base BASE_BRANCH \
  --head BRANCH_NAME \
  --title "[TASK_KEY] <concise description from task brief>" \
  --body "$(cat <<'EOF'
Jira: https://jodasoft.atlassian.net/browse/TASK_KEY

## What changed
- bullet 1
- bullet 2
- bullet 3

## Test plan
`scripts/validate-tests` passes — unit tests and headless E2E tests.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Capture the PR URL from the output.

Optional (silent fail): `mcp__github__request_copilot_review`
Optional (silent fail): set Jira status to "In Review" via `mcp__jira__editJiraIssue`

---

### Phase 5 — Reviewer, first pass (Sonnet)

Before spawning the Reviewer, capture the current commit SHA — this is the **reviewer baseline**
used by all subsequent scoped Reviewer passes:

```
git rev-parse HEAD
```

Store this as `REVIEWER_BASELINE`.

Spawn a Reviewer agent with `model: claude-sonnet-4-6`. Pass it the PR URL, branch, and base branch.

Reviewer instructions:

> You are the Reviewer. Perform a thorough code review on the changes in this PR.
>
> **PR URL:** PR_URL
> **Branch:** BRANCH_NAME
> **Base branch:** BASE_BRANCH
>
> ```
> git checkout BRANCH_NAME
> git diff BASE_BRANCH..HEAD
> ```
>
> Review for:
> - Correctness and completeness against the task's exit criteria (read the PR description
>   for context on what was intended)
> - CLAUDE.md conventions: `[LoggerMessage]` source-gen only in `MessageLogger.cs`, no
>   direct `logger.LogXxx()` calls, correct log event ID ranges
> - No accessibility regressions (vision > speech > eye-gaze > keyboard)
> - Code quality: simplicity, no premature abstractions, no unnecessary error handling or
>   validation, no stale or redundant comments
> - Test coverage: new logic should have unit tests; user-visible behaviour should have
>   headless E2E coverage
> - `.editorconfig` compliance
>
> For each issue found, post a comment **directly to the PR**:
> ```
> gh pr review PR_URL --comment -b "path/to/File.cs:LINE — your comment"
> ```
>
> After posting all comments, return a JSON array. Return only the JSON — no other text.
> An empty array means no issues.
>
> ```json
> [
>   {
>     "file": "relative/path/to/File.cs",
>     "line": 42,
>     "comment": "description of the issue"
>   }
> ]
> ```

**If Reviewer returns an empty array:** the cycle is complete — go to the Completion step.

**If Reviewer returns comments:** proceed to Phase 6.

---

### Phase 6 — Developer, review pass (Sonnet)

Spawn a Developer agent with `model: claude-sonnet-4-6`. Pass it the PR URL, branch, and task brief.

Developer instructions:

> You are the Developer. Address the open review comments on this PR.
>
> **Task key:** TASK_KEY
> **Branch:** BRANCH_NAME
> **PR URL:** PR_URL
>
> **Task brief:**
> TASK_BRIEF
>
> **Step 1** — Check out the branch:
> ```
> git checkout BRANCH_NAME
> ```
>
> **Step 2** — Read all PR comments:
> ```
> gh pr view PR_URL --comments
> ```
>
> **Step 3** — For each comment, either:
> a) Implement the fix, OR
> b) If the comment is factually incorrect or the fix is genuinely not appropriate, post a
>    rebuttal reply directly to the PR (explain the reasoning concisely):
>    ```
>    gh pr review PR_URL --comment -b "File.cs:LINE — [your rebuttal]"
>    ```
>
> **Step 4** — Build, commit, and push:
> ```
> scripts/validate-build.sh
> git commit -m "review: address feedback [TASK_KEY]"
> git push
> ```
>
> Return: `{ "status": "done", "branch": "BRANCH_NAME" }`

After Developer finishes, update `REVIEWER_BASELINE` to the current HEAD commit:

```
git rev-parse HEAD
```

Then spawn **Tester and scoped Reviewer in parallel** and wait for both.

**Tester** (Haiku) — same instructions as Phase 3.

**Scoped Reviewer** (Sonnet) instructions:

> You are the Reviewer performing a follow-up review.
>
> **PR URL:** PR_URL
> **Branch:** BRANCH_NAME
> **Reviewer baseline commit:** REVIEWER_BASELINE
>
> This is not a full re-review. Focus only on:
>
> 1. **Previous comments** — read the PR comments (`gh pr view PR_URL --comments`) and verify
>    every previously-raised issue is either fixed or has an accepted rebuttal. If any remain
>    unaddressed without a rebuttal, include them in your output.
>
> 2. **Changed files** — review only files that changed since the baseline commit:
>    ```
>    git checkout BRANCH_NAME
>    git diff REVIEWER_BASELINE..HEAD
>    ```
>    Raise new issues only if they are **significant**: correctness bugs, security problems,
>    accessibility regressions, or clear spec non-compliance. Do not raise style, naming,
>    or minor cleanup issues.
>
> For each issue, post a comment directly to the PR:
> ```
> gh pr review PR_URL --comment -b "path/to/File.cs:LINE — your comment"
> ```
>
> Return a JSON array (same schema as before). Return only the JSON — no other text.
> An empty array means all previous comments are resolved and no new significant issues exist.

**Routing after both complete:**
- If Tester has failures → re-spawn Developer with failure list (same-failure-3x guard applies)
- If scoped Reviewer has issues → re-spawn Developer with PR URL to address them
- If both have issues → re-spawn Developer once with both sets
- If both are clean → go to Completion

Loop, updating `REVIEWER_BASELINE` each time before the parallel spawn.

---

## Completion

When the Reviewer returns an empty array, tell the user:

> Implementation complete.
> PR: PR_URL
> All tests pass and all review comments have been addressed or rebutted on the PR.
