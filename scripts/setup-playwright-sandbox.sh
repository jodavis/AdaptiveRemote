#!/usr/bin/env bash
# Sets up Playwright browser symlinks in Claude Code cloud sandbox environments,
# where cdn.playwright.dev is blocked and browsers are pre-installed at /opt/pw-browsers
# under a different revision than the current Playwright package expects.
#
# Usage: bash scripts/setup-playwright-sandbox.sh
# Then run tests with: PLAYWRIGHT_BROWSERS_PATH=/opt/pw-browsers dotnet test ...

set -euo pipefail

BROWSERS_PATH=/opt/pw-browsers
HEADLESS_PROJECT=test/AdaptiveRemote.EndToEndTests.Host.Headless

if [ ! -d "$BROWSERS_PATH" ]; then
    echo "ERROR: $BROWSERS_PATH not found. This script is for Claude Code cloud sandbox environments only."
    exit 1
fi

# Build the headless project to ensure the Playwright package is restored
echo "Building headless host to restore Playwright package..."
dotnet build src/AdaptiveRemote.Headless/AdaptiveRemote.Headless.csproj -v q

PLAYWRIGHT_JS="$HEADLESS_PROJECT/bin/Debug/net10.0/.playwright/package/lib/server/registry/index.js"
if [ ! -f "$PLAYWRIGHT_JS" ]; then
    echo "ERROR: Playwright registry not found at $PLAYWRIGHT_JS. Did the build succeed?"
    exit 1
fi

# Ask Playwright's own registry what version it expects; exits with error if the result
# doesn't look like a path under $BROWSERS_PATH (guards against printing exception text).
get_expected_dir() {
    local browser_name="$1"
    local result
    result=$(node -e "
process.env.PLAYWRIGHT_BROWSERS_PATH = '$BROWSERS_PATH';
const { registry } = require('./$PLAYWRIGHT_JS');
const execs = registry._executables.filter(e => e.name === '$browser_name');
if (execs.length === 0) { process.exit(1); }
const exec = execs[0];
try { console.log(exec.executablePath()); } catch(e) { process.exit(1); }
" 2>/dev/null) || { echo "WARNING: Could not determine expected path for '$browser_name'" >&2; return 1; }

    if [[ "$result" != "$BROWSERS_PATH"/* ]]; then
        echo "WARNING: Unexpected path for '$browser_name': $result" >&2
        return 1
    fi
    echo "$result"
}

# Find the highest installed revision directory matching the given prefix
find_installed() {
    local prefix="$1"
    ls -d "$BROWSERS_PATH/$prefix"* 2>/dev/null | grep -v "INSTALLATION_COMPLETE\|DEPENDENCIES" | sort -V | tail -1
}

setup_browser() {
    local expected_exec="$1"    # e.g. /opt/pw-browsers/chromium-1208/chrome-linux64/chrome
    local installed_exec="$2"   # e.g. /opt/pw-browsers/chromium-1194/chrome-linux/chrome
    local expected_dir
    expected_dir=$(dirname "$expected_exec")
    local marker_dir
    marker_dir=$(dirname "$expected_dir")

    if [ -f "$installed_exec" ]; then
        echo "Linking: $expected_exec -> $installed_exec"
        mkdir -p "$expected_dir"
        ln -sf "$installed_exec" "$expected_exec"
        touch "$marker_dir/INSTALLATION_COMPLETE"
    else
        echo "WARNING: installed binary not found at $installed_exec — skipping"
    fi
}

# Chromium (full browser)
CHROMIUM_INSTALLED_DIR=$(find_installed "chromium-")  # matches chromium-NNNN dirs only (not chromium_headless_shell-*)
if [ -n "$CHROMIUM_INSTALLED_DIR" ] && CHROMIUM_EXPECTED=$(get_expected_dir "chromium"); then
    CHROMIUM_INSTALLED_BIN=$(find "$CHROMIUM_INSTALLED_DIR" -name "chrome" -not -name "*.sh" | head -1)
    [ -n "$CHROMIUM_INSTALLED_BIN" ] && setup_browser "$CHROMIUM_EXPECTED" "$CHROMIUM_INSTALLED_BIN"
fi

# Chromium headless shell (used by the .NET Headless host)
HEADLESS_INSTALLED_DIR=$(find_installed "chromium_headless_shell-")
if [ -n "$HEADLESS_INSTALLED_DIR" ] && HEADLESS_EXPECTED=$(get_expected_dir "chromium-headless-shell"); then
    HEADLESS_INSTALLED_BIN=$(find "$HEADLESS_INSTALLED_DIR" -name "headless_shell" -o -name "chrome-headless-shell" 2>/dev/null | head -1)
    [ -n "$HEADLESS_INSTALLED_BIN" ] && setup_browser "$HEADLESS_EXPECTED" "$HEADLESS_INSTALLED_BIN"
fi

echo ""
echo "Done. Run E2E tests with:"
echo "  PLAYWRIGHT_BROWSERS_PATH=$BROWSERS_PATH dotnet test $HEADLESS_PROJECT/AdaptiveRemote.EndToEndTests.Host.Headless.csproj"
