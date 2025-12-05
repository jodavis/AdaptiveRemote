
# Projects Overview

This document describes the high-level organization of the AdaptiveRemote repository. It is intended for new developers and Copilot agents to understand where code should be implemented, the responsibilities and boundaries of each project, and conventions for cross-platform support.

## Project Structure and Responsibilities

### AdaptiveRemote.App
- **Purpose:** The main application logic, shared UI (Blazor), conversation/speech, device interactions, and all cross-platform features.
- **Guidance:** _All new features, services, and UI components should be implemented here._ This is the only place for shared logic. Avoid any platform-specific code or dependencies in this project.
- **Boundaries:**
	- No Windows-only APIs or platform-specific features.
	- All business logic, device abstraction, and UI should be here.
	- Platform hosts should only contain minimal startup/bootstrapping code.

### AdaptiveRemote
- **Purpose:** Windows-only WPF host for AdaptiveRemote.App. This is the main target platform for end users.
- **Responsibilities:**
	- Minimal code to start the app on Windows.
	- Handles Windows-specific integrations (e.g., Windows Speech Services).
	- _Do not add business logic or features here._

### AdaptiveRemote.Electron (TBD)
- **Purpose:** Cross-platform Electron host for AdaptiveRemote.App, supporting both Windows and Linux.
- **Responsibilities:**
	- Minimal code to start the app in Electron.
	- Used for automated testing in Linux runners (e.g., GitHub Actions, Copilot Agents).
	- _No platform-specific features should be implemented here._

### AdaptiveRemote.Console
- **Purpose:** Console-mode launcher for AdaptiveRemote (Windows), useful for debugging and logging.
- **Responsibilities:**
	- Minimal code to launch the WPF app with console logging.
	- No business logic or features.

## Test Projects

### AdaptiveRemote.App.Tests
- **Purpose:** Unit tests for core application logic and services.
- **Scope:** Should not test host-specific code.

### AdaptiveRemote.Speech.Tests
- **Purpose:** Tests for speech/grammar features, which require Windows Speech Services. As a result, these tests are not cross-platform.
- **Scope:** Any tests that cannot run without Windows dependencies.

## Cross-Platform and Implementation Guidance

- **All new features and shared logic must go in AdaptiveRemote.App.**
- **Avoid Windows-only APIs or platform-specific code in AdaptiveRemote.App.**
- **Platform hosts (AdaptiveRemote, AdaptiveRemote.Electron, AdaptiveRemote.Console) should only contain minimal startup code.**
- **AdaptiveRemote.Electron is a true cross-platform host (Windows and Linux), but is only used for automated testing, not for end users.**
- **AdaptiveRemote (WPF) is the main user-facing app and may use Windows-specific features (e.g., Windows Speech Services).**

If in doubt, implement in AdaptiveRemote.App unless the code is strictly required for platform startup or integration.