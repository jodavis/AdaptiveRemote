---
description: Respond to human reviewer comments on a GitHub PR. Reads unresolved human-authored review comments, identifies new guidelines, asks clarifying questions via the PR thread, and outputs suggested additions to CONTRIBUTING.md. Used in the human-in-the-loop review pipeline (not dev-team).
---

You are responding to human review comments on PR $PR_URL for work item $WORK_ITEM_ID.

---

## Step 1 — Read the PR review comments

Using the GitHub MCP, fetch all review comments on the PR. Identify which comments were
left by a human reviewer (not the automated reviewer agent). Focus on unresolved threads.

## Step 2 — Categorise each comment

For each human comment, determine:

1. **Is it a project guideline?** Does the comment point out something that should
   always be done (or never done) in this codebase? If so, it is a candidate for
   `CONTRIBUTING.md`.

2. **Is it task-specific?** Does the comment only apply to this particular change and
   would not generalise to other code? If so, it is not a guideline candidate.

3. **Is it ambiguous?** Does the comment require clarification before you can determine
   whether it is a guideline? If so, post a question.

## Step 3 — Ask clarifying questions

For any comment that is ambiguous, post a reply in the PR thread via the GitHub MCP asking
a specific, narrow question. For example:

- "Is this a project-wide requirement, or specific to this service?"
- "Should this pattern apply to all async methods, or only public APIs?"

Do not ask questions about comments you can already categorise clearly.

## Step 4 — Output suggested guidelines

For each comment that is clearly a project guideline, output a suggested addition to
`CONTRIBUTING.md` in this format:

---
**Section:** [which Code Guidelines section this belongs in]
**Suggested addition:**
> [exact text to add, written in the same style as existing CONTRIBUTING.md guidelines]
**Rationale:** [why this should be a permanent guideline]
---

Do not modify `CONTRIBUTING.md` directly. Present the suggestions for human approval first.
