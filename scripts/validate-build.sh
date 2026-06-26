#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR/.."
echo 'Staging untracked files...'
git add -A
echo 'Cleaning src and test projects...'
git clean -xdf src test
echo 'Building solution...'
dotnet build /warnaserror
