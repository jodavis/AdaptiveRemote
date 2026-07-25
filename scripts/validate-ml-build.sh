#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR/../ml"
echo 'Running mypy --strict over pipeline and test...'
mypy --strict pipeline test
