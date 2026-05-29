
# AdaptiveRemote.Headless Design Document

Summary: Describes the headless console host that runs Blazor under Playwright for cross-platform automated E2E testing.

## Purpose
AdaptiveRemote.Headless is a cross-platform .NET console host for the AdaptiveRemote.App Blazor application, designed to enable automated end-to-end (E2E) UI testing in headless environments (e.g., GitHub Copilot Agents, CI/CD pipelines, and Linux containers). It allows tests to validate AdaptiveRemote functionality without requiring a graphical desktop or Windows UI stack.

## Key Requirements
- **Cross-platform:** Must run on Linux, Windows, and macOS (where supported by .NET and Playwright).
- **Headless-first:** Runs the Blazor app in a headless browser using Playwright. No visible UI is required.
- **In-process hosting:** Hosts the Blazor app in-process, following the pattern of other hosts and using `AcceleratedServices` for initialization.
- **Logging and diagnostics:** Captures Playwright screenshots and traces on failure for debugging.
- **.NET configuration:** Uses .NET Generic Host configuration, supporting command-line args, config files, and environment variables.

## Architecture Overview
1. **Startup:**
    - Console application entry point.
    - Initializes .NET Generic Host, loads configuration from all standard sources.
    - Sets up logging to stdout (console).
    - Initializes application services via `AcceleratedServices` (mirroring other hosts).

2. **Blazor App Hosting:**
    - Hosts the AdaptiveRemote Blazor app in-process (not as a separate web server process).
    - Listens on a configurable port (default: random/ephemeral or as specified in config).

3. **Playwright Integration:**
    - Launches a headless browser instance (Chromium).
    - The browser lifecycle is managed by [`PlaywrightBrowserLifetimeService`](./PlaywrightBrowserLifetimeService.cs) which inherits from `BackgroundService`. This ensures proper cleanup on graceful shutdown, but cannot guarantee cleanup if the process is killed abruptly (see Limitations).
    - Waits for the ASP.NET application to start before launching the browser.
    - Navigates to the hosted Blazor app.
    - Browser control can be exposed to test services in future iterations via dependency injection.
    - Captures screenshots and traces on test failure.

## Usage in CI and Copilot Agent Environments
- The host is launched by the test runner (e.g., AdaptiveRemote.EndToEndTests).
- The test project connects via StreamJsonRpc to control both the app and the browser.
- On test failure, screenshots and Playwright traces are saved to a configurable output directory.

## Extensibility and Future Work
- The same StreamJsonRpc-based approach can be extended to connect Playwright to other hosts (e.g., BlazorWebView in AdaptiveRemote or AdaptiveRemote.Electron) for unified E2E testing.
- Accessibility and ARIA validation can be added later using Playwright’s accessibility APIs.

## Limitations
- If the host process is killed abruptly (e.g., SIGKILL), Playwright and browser resources may not be cleaned up. Graceful shutdown (SIGTERM, Ctrl+C) is supported and recommended in CI.
- Headless Chromium is used by default for maximum compatibility. Microsoft Edge headless is not available cross-platform, but Chromium is very close to Edge (same engine).
