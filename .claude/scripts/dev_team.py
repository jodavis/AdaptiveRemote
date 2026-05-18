#!/usr/bin/env python3
"""dev-team pipeline orchestrator.

Entry point: main() — accepts a Jira work item ID, finds the matching spec file,
and invokes the researcher agent with the researcher-plan skill.
"""

import json
import subprocess
import sys
from pathlib import Path

MODEL_MAP = {
    "haiku": "claude-haiku-4-5-20251001",
    "sonnet": "claude-sonnet-4-6",
    "opus": "claude-opus-4-7",
}


def _find_repo_root() -> Path:
    """Walk up from this file until a directory containing .claude/ is found."""
    current = Path(__file__).resolve().parent
    while True:
        if (current / ".claude").is_dir():
            return current
        parent = current.parent
        if parent == current:
            raise RuntimeError(
                f"Could not locate repo root: no .claude/ directory found "
                f"in any ancestor of {Path(__file__).resolve()}"
            )
        current = parent


REPO_ROOT = _find_repo_root()


def _parse_frontmatter(text: str) -> tuple[dict, str]:
    """Split YAML frontmatter from body. Returns (metadata_dict, body).

    Frontmatter is delimited by lines containing only '---'.
    If no frontmatter is present, returns ({}, text).
    """
    lines = text.split("\n")
    if not lines or lines[0].strip() != "---":
        return {}, text

    end = None
    for i, line in enumerate(lines[1:], start=1):
        if line.strip() == "---":
            end = i
            break

    if end is None:
        return {}, text

    frontmatter_lines = lines[1:end]
    body = "\n".join(lines[end + 1:]).lstrip("\n")

    metadata: dict = {}
    for line in frontmatter_lines:
        if ":" in line:
            key, _, value = line.partition(":")
            metadata[key.strip()] = value.strip()

    return metadata, body


def call_agent(agent_name: str, skill_name: str, *args: str, stream: bool = True) -> str:
    """Invoke a Claude agent with a skill via the claude CLI.

    Reads the agent definition for its model and system prompt, reads the skill
    definition for its instructions, and calls `claude -p` with the combined prompt.

    Args:
        agent_name: Name of the agent (matches .claude/agents/<name>.md).
        skill_name: Name of the skill (matches .claude/commands/<name>.md).
        *args:      Arguments passed to the skill, substituted for $ARGUMENTS.
        stream:     If True (default), print output to stdout as it arrives.
                    Set to False for agents that return structured JSON.

    Returns:
        The full text output from the agent.

    Raises:
        FileNotFoundError: Agent or skill definition file not found.
        RuntimeError: claude CLI not on PATH, or unexpected output format.
        subprocess.CalledProcessError: claude CLI exited with non-zero status.
    """
    agent_path = REPO_ROOT / ".claude" / "agents" / f"{agent_name}.md"
    skill_path = REPO_ROOT / ".claude" / "commands" / f"{skill_name}.md"

    if not agent_path.exists():
        raise FileNotFoundError(
            f"Agent definition not found: .claude/agents/{agent_name}.md"
        )
    if not skill_path.exists():
        raise FileNotFoundError(
            f"Skill definition not found: .claude/commands/{skill_name}.md"
        )

    agent_meta, agent_body = _parse_frontmatter(agent_path.read_text(encoding="utf-8"))
    _, skill_body = _parse_frontmatter(skill_path.read_text(encoding="utf-8"))

    arguments_str = " ".join(args)
    skill_body = skill_body.replace("$ARGUMENTS", arguments_str)

    prompt = f"{agent_body}\n\n---\n\n{skill_body}"

    raw_model = agent_meta.get("model", "sonnet")
    model = MODEL_MAP.get(raw_model, raw_model)

    try:
        proc = subprocess.Popen(
            ["claude", "-p", prompt, "--model", model],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            cwd=REPO_ROOT,
        )
    except FileNotFoundError:
        raise RuntimeError(
            "claude CLI not found on PATH. "
            "Ensure Claude Code is installed and claude.exe is accessible."
        )

    chunks: list[str] = []
    for line in proc.stdout:  # type: ignore[union-attr]
        if stream:
            print(line, end="", flush=True)
        chunks.append(line)

    proc.wait()
    stderr_text = proc.stderr.read()  # type: ignore[union-attr]

    if proc.returncode != 0:
        raise subprocess.CalledProcessError(
            proc.returncode,
            proc.args,
            stderr=f"{stderr_text}\n(exit code {proc.returncode})",
        )

    return "".join(chunks)


def find_spec_file(work_item_id: str) -> Path:
    """Find the unique _spec_*.md file that contains the work item ID.

    Raises SystemExit(1) with a clear message on zero or multiple matches.
    """
    candidates = [
        p
        for p in REPO_ROOT.rglob("_spec_*.md")
        if ".git" not in p.parts
    ]

    matches = [
        p for p in candidates
        if work_item_id in p.read_text(encoding="utf-8")
    ]

    if not matches:
        print(
            f"Error: no _spec_*.md file found containing '{work_item_id}'.\n"
            f"Verify the task key is correct and you are on the right branch.",
            file=sys.stderr,
        )
        sys.exit(1)

    if len(matches) > 1:
        paths = "\n  ".join(str(m.relative_to(REPO_ROOT)) for m in matches)
        print(
            f"Error: multiple spec files found containing '{work_item_id}' — "
            f"cannot determine which to use:\n  {paths}\n"
            f"Resolve the ambiguity (e.g. deduplicate the task key) and retry.",
            file=sys.stderr,
        )
        sys.exit(1)

    return matches[0]


def main() -> None:
    if len(sys.argv) < 2:
        print(
            "Usage: dev_team.py <work-item-id>  (e.g. dev_team.py ADR-172)",
            file=sys.stderr,
        )
        sys.exit(1)

    work_item_id = sys.argv[1]

    print(f"Searching for spec for {work_item_id}", flush=True)
    spec_file = find_spec_file(work_item_id)
    spec_path = str(spec_file.relative_to(REPO_ROOT))
    print(f"Found {spec_file}", flush=True)

    try:
        print(f"Researcher is planning work for {work_item_id}...", flush=True)
        call_agent("researcher", "researcher-plan", work_item_id, spec_path)
    except (FileNotFoundError, RuntimeError, subprocess.CalledProcessError) as e:
        print(f"Error invoking researcher agent:\n{e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()
