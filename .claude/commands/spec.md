---
description: Draft a _spec_*.md design document for a new feature, iterating with the user until the document is ready
argument-hint: <feature name and description>
---

## Feature to spec

$ARGUMENTS

## Available architecture docs

!`find . -name "_doc_*.md" -not -path "./.git/*" | sort`

## Workflow

This is an iterative process. Follow the phases below. Never skip ahead —
always wait for user input at each pause point before continuing.

---

### Phase 1 — Orient and gather context

Read the relevant `_doc_*.md` files from the list above. Read all that apply
to the feature area; at minimum read `src/_doc_Projects.md`. Also read any
relevant source code in the workspace.

Then ask the user a focused set of questions to fill gaps that the docs and
feature description don't answer. Good questions cover:

- Ownership and boundaries (what this feature owns vs. delegates)
- Integration points with existing subsystems
- Key design choices where multiple reasonable approaches exist
- Constraints (performance, accessibility, testability requirements)
- Anything the planned implementation section will need to be concrete

Ask all your questions at once — don't ask one at a time. Skip questions
you can already answer from the docs, feature description, or source code.

**PAUSE — wait for the user's answers before continuing.**

If the answers raise new ambiguities that would materially affect the spec,
ask one more targeted follow-up round. Otherwise proceed to Phase 2.

---

### Phase 2 — First draft

Determine the spec file location: the `_spec_*.md` lives next to the code it
describes — in the directory where the new feature's code will live. Use the
project boundaries doc if uncertain.

Name: `_spec_<FeatureName>.md` in PascalCase

Draft and write the file using the structure at the end of this prompt.
Fill every section. For anything genuinely unresolved, use `> TBD: reason`
inline and list it again in Open Questions.

After writing, tell the user:

> Draft written to `<path>`. Please review it — edit any section directly
> and add `> **Review:** your comment or question` anywhere you want a
> change made or a question answered. Tell me when you're ready for the
> next pass.

**PAUSE — wait for the user to review and signal readiness.**

---

### Phase 3 — Iterative refinement

When the user signals they're ready:

1. Re-read the spec file with the Read tool.
2. Collect all `> **Review:** ...` markers and note any direct edits.
3. If any review comment is ambiguous or requires a design decision,
   ask clarifying questions first (again, all at once).
   **PAUSE if you asked questions — wait for answers before editing.**
4. Update the spec: apply changes, resolve review markers by removing them
   or incorporating the feedback, and update Open Questions accordingly.
5. Tell the user what changed and invite another review pass.

Repeat Phase 3 until the user says the document is ready.

---

### Phase 4 — Implementation readiness review

When the user says the document is ready:

1. Re-read the spec from the perspective of an agent assigned to implement
   it — one that has no context beyond what is written here. Ask: could you
   implement every part without guessing at what is wanted? Include test
   coverage in scope: if you could not write a unit test or E2E scenario
   without guessing at the expected behavior, that is a gap.
2. If you find gaps — missing decisions, ambiguous behavior, unspecified
   error cases, unclear interfaces — list them all and ask clarifying
   questions (all at once).
   **PAUSE if you asked questions — wait for answers before editing.**
3. Update the spec to fill the gaps from the user's answers.

Repeat until you have no remaining questions that would require guessing
to implement. Then tell the user the spec is implementation-ready and
proceed to Phase 5.

---

### Phase 5 — Task breakdown and Jira tickets

When Phase 4 is complete:

1. Ask: "Is there a Jira epic for this feature? If so, share the epic key."
   **PAUSE — wait for the answer before continuing.**
2. Add a `## Tasks` section at the end of the spec file. Break the work
   into tasks sized to roughly one PR each. For each task write:
   - A short title
   - A one-sentence description
   - Exit criteria as a checkbox list; for tasks that include new E2E tests,
     write those exit criteria as Gherkin-style acceptance scenarios
     (`Given / When / Then`)
3. Save the updated spec and ask the user to review the task breakdown.
   **PAUSE — wait for approval or change requests. Apply any changes
   before proceeding.**
4. Create a Jira task for each item. If the user provided an epic key,
   assign it as the parent of all tasks. If not, create tasks without a
   parent — the parent can be added later.
5. Update the `## Tasks` section: replace each task title with a hyperlink
   to its Jira ticket. Keep all descriptions and exit criteria in place.
   The section remains in the spec permanently — future agents may not
   have Jira access.

---

## Spec file structure

Use this structure for the `_spec_*.md` file:

---

# \<Feature Name\>

> **Status:** Draft
> **Will become:** `_doc_<FeatureName>.md` once implementation is complete

## Overview

One paragraph: what this feature does and why it exists.

## Responsibilities & Boundaries

- **Owns:** ...
- **Does not own:** ...
- **Integrates with:** ...

## Key Design Decisions

### \<Decision title\>

_Context:_ Why this choice was needed.
_Decision:_ What was decided.
_Consequences:_ Trade-offs accepted.

_(Repeat for each significant decision.)_

## Planned Implementation

### Interfaces

Public interfaces — method signatures, types, and responsibilities.
This section is more detailed than a `_doc_` file because the source
doesn't exist yet.

### Key Classes

Planned classes, their roles, and important relationships.

### Data Flow

How data moves through the feature from trigger to output.

## Open Questions

- [ ] Unresolved question (carry forward any unresolved TBDs from above)

## Related Docs

Links to the `_doc_*.md` files consulted during drafting.
