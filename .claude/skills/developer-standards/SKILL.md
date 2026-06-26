---
name: developer-standards
description: >
  Use when planning new code, writing code, or reviewing code.
  Loads project code guidelines and quality gates from CONTRIBUTING.md and CLAUDE.md.
---

Use this skill when:
- You are planning new code, writing code, or reviewing code

## Steps

### 1 — Read code guidelines

Read `CONTRIBUTING.md` for code guidelines: naming conventions, file structure, logging standards, and test conventions.

### 2 — Read quality gates

Read `CLAUDE.md` for quality gates and operational conventions specific to this repo.

### 3 — Read `.editorconfig`

Read `.editorconfig` and treat it as the authoritative code style specification. Follow every rule
exactly as written — including indentation style, tab width, line endings, charset, trailing
whitespace, final newlines, and any file-type-specific sections. Do not override any setting.

Apply all three sets of standards to every file you write or review.
