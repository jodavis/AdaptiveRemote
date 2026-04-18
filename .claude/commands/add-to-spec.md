---
description: Add a new requirement or task to an existing _spec_*.md file, iterating with the user until the content is ready
argument-hint: <Jira issue key or brief description of the new requirement>
---

## Requirement to add

Jira Task $ARGUMENTS

## Available spec files

!`find . -name "_spec_*.md" -not -path "./.git/*" | sort`

## Available architecture docs

!`find . -name "_doc_*.md" -not -path "./.git/*" | sort`

## Workflow

This is an iterative process. Follow the phases below. Never skip ahead —
always wait for user input at each pause point before continuing.

---

### Phase 1 — Orient and gather context

1. Identify the target spec file. If ARGUMENTS contains a Jira key, fetch the issue.
   The target spec should be determined by the Jira work item's parent.
   If the target spec is not obvious, ask the user which spec to add to.
2. Read the spec file in full. Note: existing tasks, task numbering, insertion point,
   and patterns (checklist format, Gherkin scenarios, exit criteria structure).
3. Read any `_doc_*.md` files relevant to the area the new requirement touches.

Then ask the user a focused set of questions to fill gaps the Jira description and spec
don't answer. Good questions cover:

- Scope boundaries (what this task owns vs. defers)
- Integration points with existing tasks or services
- Key design choices where multiple reasonable approaches exist
- Whether new patterns established here must be retrofitted to earlier tasks

Ask all your questions at once. Skip anything you can already answer.

**PAUSE — wait for the user's answers before continuing.**

If answers raise new ambiguities that materially affect the content, ask one more targeted
follow-up round. Otherwise proceed to Phase 2.

---

### Phase 2 — Draft the new content

1. Draft any new spec sections and update existing sections to reflect new decisions.
2. Draft the new task section using the existing spec's task format:
   - `### Task N — <title> ([ADR-XXX](<url>))`
   - One-paragraph description
   - Checklist items (`- [ ] ...`)
   - Gherkin-style acceptance scenarios for any items that describe observable behavior
3. Identify any exit criteria that should be added to other tasks:
   - Patterns this task establishes that all future tasks must follow
   - Stubs or placeholders in earlier tasks that this task replaces
4. Determine the new task number and which existing tasks (if any) need renumbering.

Write the draft to the spec file:
- Insert the new task at the correct position
- Update any other tasks with new exit criteria
- Renumber task headings if needed (ADR links and checklist content are never changed
  by renumbering — only the `### Task N —` prefix)

After writing, tell the user:

> Draft written to `<path>`. Please review it — edit any section directly and add
> `> **Review:** your comment or question` anywhere you want a change made. Tell me
> when you're ready for the next pass.

**PAUSE — wait for the user to review and signal readiness.**

---

### Phase 3 — Iterative refinement

When the user signals they're ready:

1. Re-read the spec file.
2. Collect all `> **Review:** ...` markers and any direct edits.
3. Address review comments **one at a time** in document order:
   a. Present your analysis — trade-offs and recommendation.
   b. **PAUSE — wait for the user's decision before editing.**
   c. Update the spec; remove the review marker.
   d. State what changed; move to the next comment.
4. After all comments are resolved, invite another review pass.

Repeat Phase 3 until the user says the content is ready.

---

### Phase 4 — Implementation readiness review

Read the requirements from the perspective of a code agent assigned to implement them with
no additional context. Ask:

- Can you implement every checklist item without guessing?
- Can you write unit tests and Gherkin scenarios without guessing at expected behavior?
- Are there missing error cases, unspecified interfaces, or ambiguous behavior?
- Does new content interact with existing tasks in ways that need to be reflected there
  (e.g., a new pattern that earlier tasks must also adopt, or a stub that a later task
  will replace)?

If you find gaps, list them all and ask clarifying questions (all at once).

**PAUSE if you asked questions — wait for answers before continuing.**

Otherwise proceed to Phase 5.

---

### Phase 5 — Jira update

Update the Jira task's description: replace initial design notes with a concise summary of
the finalized spec content. Include:
- A one-paragraph overview of what this task implements
- A bulleted list of key decisions and their outcomes
- A reference to the spec file: `See spec: <relative path>`

The original Jira description may contain early "initial thoughts" that are now superseded;
replace it entirely rather than appending.
