#!/usr/bin/env bash
# Runs once after container creation and workspace mount (as the vscode user).
# Heavy deps are already in the image; this only handles repo-dependent steps.
set -euo pipefail

WORKSPACE_DIR="/workspaces/AdaptiveRemote"
cd "$WORKSPACE_DIR"

echo "==> [post-create] Step 1: Restoring NuGet packages..."
dotnet restore

echo "==> [post-create] Step 2: Building solution (emits playwright.ps1)..."
# /warnaserror omitted intentionally: this is not a quality gate.
dotnet build --no-restore

echo "==> [post-create] Step 3: Installing Playwright Chromium browser..."
PLAYWRIGHT_PS1="$WORKSPACE_DIR/src/AdaptiveRemote.Headless/bin/Debug/net10.0/playwright.ps1"

if [ -f "$PLAYWRIGHT_PS1" ]; then
    pwsh "$PLAYWRIGHT_PS1" install chromium --with-deps
else
    echo "==> [post-create] WARNING: playwright.ps1 not found; falling back to dotnet tool..."
    dotnet tool install --global Microsoft.Playwright.CLI 2>/dev/null || true
    # Global tools install to ~/.dotnet/tools which may not be on PATH yet.
    export PATH="$HOME/.dotnet/tools:$PATH"
    playwright install chromium --with-deps
fi

echo "==> [post-create] Step 4: Seeding Claude Code user settings (first-run only)..."
CLAUDE_SETTINGS="$HOME/.claude/settings.json"
CLAUDE_DEFAULTS="/home/vscode/.claude/settings.json"

if [ ! -f "$CLAUDE_SETTINGS" ]; then
    cp "$CLAUDE_DEFAULTS" "$CLAUDE_SETTINGS"
    echo "==> [post-create] Installed default settings to ~/.claude/settings.json"
else
    echo "==> [post-create] ~/.claude/settings.json already exists — not overwriting."
fi

echo "==> [post-create] Done. Quality gates:"
echo "  scripts/validate-build.sh   — clean build, zero warnings"
echo "  scripts/validate-tests.sh   — unit + headless E2E tests"
