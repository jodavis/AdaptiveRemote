#!/usr/bin/env bash
# Runs all Linux-compatible quality gates. Exit code is non-zero if any gate fails.
# Excluded: Speech.Tests, Host.Wpf, Host.Console (Windows-only targets).
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

echo "=== Build ==="
dotnet build /warnaserror

echo ""
echo "=== Unit tests ==="
dotnet test test/AdaptiveRemote.App.Tests/AdaptiveRemote.App.Tests.csproj

echo ""
echo "=== Headless E2E tests ==="
dotnet test test/AdaptiveRemote.EndToEndTests.Host.Headless/AdaptiveRemote.EndToEndTests.Host.Headless.csproj

echo ""
echo "All quality gates passed."
