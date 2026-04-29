#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR/.."
echo 'Testing unit test projects...'
dotnet test --no-build "$SCRIPT_DIR/validate-unit-tests.proj"
echo 'Testing E2E test projects...'
dotnet test --no-build "$SCRIPT_DIR/validate-e2e-tests.proj" -m:1
