---
name: find-repo-documentation
description: >
  Use when you need to learn the architecture from documentation in this repo.
  Discovers available architecture docs and reads the ones relevant to the current task.
argument-hint: <task context or area to research>
---

Use this skill when:
- You need to learn the architecture from documentation in this repo

## File format and locations

Architecture documents are named `_doc_*.md` and live at the repo root and in subdirectories. Each one starts with a `Summary:` line that describes what it covers.

## Steps

### 1 — Discover available docs

Find all architecture documents and their summaries in one pass:

```bash
grep -rn "^Summary:" . --include="_doc_*.md"
```

Each output line contains a file path and its `Summary:` value. Use this output directly to build your list of available docs and their topics — no additional reads are required for this step.

### 2 — Select relevant docs

From the task context or area you have been given, identify which subsystems and topics the task will touch. Select the docs whose summaries match those areas.

### 3 — Read and summarize

Read each selected doc in full. For each one, note:
- File path
- What it says about the areas this task will touch
- Any constraints, patterns, or conventions the implementer must follow

Internalize these findings as working context for the current task. For each doc consulted, note its file path and the constraints, patterns, or conventions that apply to the work ahead.
