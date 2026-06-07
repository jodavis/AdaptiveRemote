#!/usr/bin/env python3
"""Run a claude subagent to read information about a GitHub PR using the gh CLI."""

import subprocess
import sys

prompt = "Use the gh CLI to read information about PR #202 in the jodavis/AdaptiveRemote repository and return all the details you find (title, status, author, description, comments, checks, etc.). Use: gh api repos/jodavis/AdaptiveRemote/pulls/202"

result = subprocess.run(
    [
        "claude",
        "-p",
        "--allowedTools", "Bash(gh *)",
    ],
    input=prompt,
    capture_output=True,
    text=True,
)

if result.returncode != 0:
    print("Error:", result.stderr, file=sys.stderr)
    sys.exit(result.returncode)

print(result.stdout)
