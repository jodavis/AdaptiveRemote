---
description: Produce a concise task brief for a GitHub issue, synthesized from the issue body and comments, the relevant architecture docs, and source code. Proposes exit criteria since none are written in the issue.
argument-hint: <GH-NNN work item ID>
---

## Inputs

Work item ID: the first token of `$ARGUMENTS` (e.g. `GH-444`)

If missing, stop and tell the caller:

> Usage: `/researcher-issue <GH-NNN>`

## Debug report

A root-cause report from the prior debugging step is embedded below. Use its findings —
confirmed root cause, evidence, ruled-out hypotheses — as supporting context when
proposing exit criteria and identifying affected files. If the section is empty, the
debugging step did not run or produced no output.

$DEBUG_REPORT

---

## Steps

### 1 — Fetch the GitHub issue

Derive the issue number by stripping the `GH-` prefix from the work item ID
(e.g. `GH-444` → `444`). Fetch the issue title, body, and all comments:

```bash
gh issue view <number> --repo jodasoft/AdaptiveRemote --comments
```

If the issue is not found, stop and report the error.

### 2–5 — Exploration and brief

Read `.claude/commands/researcher-plan.md` and follow **steps 2 through 5** exactly, with
two differences:

- **Source of requirements (step 1 replacement):** Use the issue title, body, and comments
  you fetched above instead of a spec file section. Let the issue content guide which
  subsystems and areas are relevant.

- **Exit criteria (step 5 difference):** The issue contains no formal exit criteria. Instead
  of copying them verbatim, **propose** a concrete, checkable list synthesized from the issue
  description and the context you gathered. Frame each criterion the same way spec exit
  criteria are written (observable behaviour, not implementation detail). Label the section
  **"Exit criteria (proposed)"** so the caller knows these are inferred, not authoritative.

- **Required opening heading (step 5 addition):** The brief must open with this exact heading
  (substitute the real work item ID):

  ```
  # Implementation plan for <work-item-id>
  ```
