
# AdaptiveRemote.Headless Design Document

## Purpose
AdaptiveRemote.Headless is a cross-platform .NET console host for the AdaptiveRemote.App Blazor application, designed to enable automated end-to-end (E2E) UI testing in headless environments (e.g., GitHub Copilot Agents, CI/CD pipelines, and Linux containers). It allows tests to validate AdaptiveRemote functionality without requiring a graphical desktop or Windows UI stack.

## Key Requirements
- **Cross-platform:** Must run on Linux, Windows, and macOS (where supported by .NET and Playwright).
- **Headless-first:** Runs the Blazor app in a headless browser using Playwright. No visible UI is required.
- **In-process hosting:** Hosts the Blazor app in-process, following the pattern of `AdaptiveRemote.Electron` and using `AcceleratedServices` for initialization.
- **Test integration:** Exposes Playwright/browser control to the test project via the existing StreamJsonRpc channel, so tests can drive the UI and validate state.
- **Logging and diagnostics:** Captures Playwright screenshots and traces on failure for debugging.
- **.NET configuration:** Uses .NET Generic Host configuration, supporting command-line args, config files, and environment variables.
- **Not a test project:** Should be a host project in `src/` (not `test/`), like other AdaptiveRemote hosts.

## Architecture Overview
1. **Startup:**
    - Console application entry point.
    - Initializes .NET Generic Host, loads configuration from all standard sources.
    - Sets up logging to stdout (console).
    - Initializes application services via `AcceleratedServices` (mirroring `AdaptiveRemote.Electron`).

2. **Blazor App Hosting:**
    - Hosts the AdaptiveRemote Blazor app in-process (not as a separate web server process).
    - Listens on a configurable port (default: random/ephemeral or as specified in config).

3. **Playwright Integration:**
    - Launches a headless browser instance (Chromium by default; configurable).
    - The browser lifecycle is managed by an `IHostedService` implementation, which starts Playwright in `InitializeAsync()` and shuts it down cleanly in `ShutDownAsync()`. This ensures proper cleanup on graceful shutdown, but cannot guarantee cleanup if the process is killed abruptly (see Limitations).
    - Navigates to the hosted Blazor app.
    - Exposes Playwright/browser control to the test project via StreamJsonRpc.
    - Captures screenshots and traces on test failure or as requested.

4. **Communication:**
    - Uses the existing StreamJsonRpc channel for test-to-app and test-to-browser communication.
    - No additional custom protocol is required; extend the channel as needed for browser control.
    - **Intent-based API:** The test service interface should expose high-level, intent-based commands (e.g., `ClickButton(string label)`, `CheckButtonIsVisible(string label)`) rather than duplicating the Playwright API. This keeps tests maintainable and decoupled from Playwright details. The TestService class will translate these commands into Playwright actions internally.

    **Example:**
    ```csharp
    // In the test service interface:
    Task ClickButton(string label);
    Task<bool> CheckButtonIsVisible(string label);
    
    // Implementation (simplified):
    public async Task ClickButton(string label)
    {
        var button = await _page.QuerySelectorAsync($"button:text('{label}')");
        if (button != null) await button.ClickAsync();
        else throw new Exception($"Button '{label}' not found");
    }
    ```

5. **Configuration:**
    - All settings (port, browser type, logging, etc.) are configurable via .NET configuration (command-line, environment, config files).

## Usage in CI and Copilot Agent Environments
- The host is launched by the test runner (e.g., AdaptiveRemote.EndToEndTests).
- The test project connects via StreamJsonRpc to control both the app and the browser.
- All logs are written to stdout/stderr for easy capture in CI logs.
- On test failure, screenshots and Playwright traces are saved to a configurable output directory.

## Extensibility and Future Work
- The same StreamJsonRpc-based approach can be extended to connect Playwright to other hosts (e.g., BlazorWebView in AdaptiveRemote or AdaptiveRemote.Electron) for unified E2E testing.
- Accessibility and ARIA validation can be added later using Playwright’s accessibility APIs.

## Project Location and Naming
- Project name: `AdaptiveRemote.Headless`
- Location: `src/AdaptiveRemote.Headless/`


## MVP Command Set
| Command                        | Description                                 |
|--------------------------------|---------------------------------------------|
| ClickButton(string label)      | Clicks a button with the given label         |
| CheckButtonIsVisible(string label) | Returns true if a button is visible         |
| CheckButtonIsEnabled(string label) | Returns true if a button is enabled         |

Additional commands can be added as test coverage expands.

## Limitations
- If the host process is killed abruptly (e.g., SIGKILL), Playwright and browser resources may not be cleaned up. Graceful shutdown (SIGTERM, Ctrl+C) is supported and recommended in CI.
- Headless Chromium is used by default for maximum compatibility. Microsoft Edge headless is not available cross-platform, but Chromium is very close to Edge (same engine).
- The intent-based API means not all Playwright features are exposed; only those needed for test intent are implemented.

## Open Questions / Next Steps
- Expand the intent-based command set as new test scenarios are identified.
- Document how to run the host and connect from tests.
