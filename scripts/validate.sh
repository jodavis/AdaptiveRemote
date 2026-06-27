#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

check_tool() {
    local tool="$1"
    if ! command -v "$tool" &>/dev/null; then
        echo "ERROR: Required tool '$tool' is not installed or not on PATH." >&2
        exit 1
    fi
}

echo 'Checking required tools...'
check_tool dotnet
check_tool pwsh
check_tool node
check_tool python3
check_tool claude
check_tool dvc

"$SCRIPT_DIR/validate-build.sh"
"$SCRIPT_DIR/validate-tests.sh"
