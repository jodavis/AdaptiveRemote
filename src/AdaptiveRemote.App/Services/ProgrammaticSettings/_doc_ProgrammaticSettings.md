# ProgrammaticSettings File Format and Usage

This document describes the file format and usage for storing programmed IR commands in AdaptiveRemote, as managed by the `ProgrammaticSettings` service.
The service is generally available for any component to add data that must be persisted across sessions. IR commands are only the first use case.

## Overview
- **Purpose:** Persistently store user-programmed IR command payloads and other configurable settings.
- **Location:** Default path is `%LocalAppData%\AdaptiveRemote\Settings.ini` (see `ProgrammaticSettings.ProgrammaticSettingsPath`).
- **Format:** INI-style key-value pairs.
- **Key Pattern:** Command name (e.g., `Power`, `VolumeUp`, `VolumeDown`) under `[IRCommands]` section
- **Value:** Base64-encoded IR data (as returned by the Broadlink device) for IR command payloads

## Example
```
[IRCommands]
Power = AABgA6gDAwQFBgcICQoLDA0ODw==
VolumeUp = AABgA6gDAwQFBgcICQoLDA0ODw==
VolumeDown = AABgA6gDAwQFBgcICQoLDA0ODw==
```

## Usage
- The settings file is included in the application's settings configuration, so it is automatically available via the IOptions API.
  - Command payloads can be provided via other settings sources as well.
    The programmatic settings file is the one that the application will write to when users program commands through the UI.
- On startup, services (like `BroadlinkCommandService`) can retrieve programmed commands via the IOptions API.
- When a user programs a new command, the IR data is stored under the command name key using the `IProgrammaticSettings` service.
- Only commands present in the `[IRCommands]` section are considered programmed and enabled in programming mode.
- The file can be pre-populated for development or testing (e.g., `Settings.sample.ini`).

## Notes
- The file may contain other sections or keys, but only keys in the `[IRCommands]` section are used for IR payloads.
- The format is designed for simplicity and easy manual editing if needed.
- For implementation details, see the `ProgrammaticSettings` class in the codebase.

## References
- `AdaptiveRemote.Services.ProgrammaticSettings.ProgrammaticSettings` (C#)
- [Spec: Programmable IR Commands](../Commands/_spec_ProgrammableCommands.md)

---
This document is intended as a file format and usage reference. For implementation details, see the source code.
