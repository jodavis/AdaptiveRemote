# Broadlink Subsystem Architecture & Design

## Overview
The Broadlink subsystem provides a driver for the Broadlink RM4 mini device, handling device discovery, authentication, and communication. It is based on the Python implementation at [mjg59/python-broadlink](https://github.com/mjg59/python-broadlink), but refactored for maintainability and debugging in C#.

## Responsibilities & Boundaries
- **Device communication:** Handles discovery, authentication, and sending IR commands to the device.
- **Command handling:** Provides handlers for [`IRCommand`](../../Models/IRCommand.cs) instances, translating command data into device protocol operations.
- **Separation of concerns:** UI, orchestration, invocation, and speech recognition for `IRCommand` instances are managed by other subsystems.

## Key Design Decisions
- **Packet objects:** Unlike the Python implementation, packet objects encapsulate buffer encoding/decoding rules, exposing get/set properties for easier debugging and unit testing.
- **Connection lifecycle:** Follows the Python model for connection and retries, but only supports the RM4 mini device (no class hierarchy for other device types).
- **OS/Framework wrappers:** Dependencies are wrapped and organized as nested factory interfaces (e.g., `ISocket` and `ISocket.Factory`) to support unit testing and reduce file proliferation.

## Testability & Maintainability
- All OS/Framework dependencies are wrapped for mocking in unit tests. (Sockets, encryption)
- Packet encoding/decoding is thoroughly unit tested.
- Debugging and unit testing are simplified by property access to packet fields.

## Extensibility
- No extensibility is expected for now.
- If new device support is needed, consider reintroducing a device class hierarchy similar to the Python project.

## Updating This Document
Update this document only when the overall design or boundaries of the Broadlink subsystem change, or when new features or device support are added. For implementation details, refer to source code and inline comments.
