# Command Architecture & Design

## Overview
The `Command` abstraction is a central data object representing a remote control command. It is designed to decouple the definition, display, execution, and invocation of commands across multiple subsystems. The base class is defined in [`Models/Command.cs`](../../Models/Command.cs).

## Responsibilities & Boundaries
- **Command is a pure data object.** It does not implement any built-in functionality or business logic.
- **Subsystems interact via Command instances:**
  - UI components read command properties to display controls.
  - Speech recognition and accessibility features reference command metadata.
  - Command services attach execution handlers (via `ExecuteAsync`). (E.g. [`LifecycleCommandService`](../Lifecycle/LifecycleCommandService.cs) or [`BroadlinkCommandService`](../Broadlink/BroadlinkCommandService.cs))
    - Command services can derive from [`CommandServiceBase`](../CommandServiceBase.cs) for common behavior, like finding commands by type, error handling and logging, and lifecycle events. This is not required but it is recommended for new command services.
- **No direct coupling:** Subsystems should not depend on each other's implementations, only on the shared `Command` abstraction.

## Lifecycle
- **Creation:** All commands are created and provided by `IRemoteDefinitionService`.
- **Modification:** Any subsystem may update command properties (e.g., enable/disable, attach handlers).
- **Ownership:** There is no explicit disposal or ownership model; commands are shared and modified as needed.

## Identification & Subclassing
- **Unique ID:** The `Name` property is a unique identifier for each command.
- **Subclassing:** Command services typically handle specific subclasses of `Command`. This avoids conflicts and ensures clear boundaries. (E.g. [`LifecycleCommand`](../../Models/LifecycleCommand.cs) or [`IRCommand`](../../Models/IRCommand.cs))
- **No conflict resolution:** It is expected that internal subsystems respect these boundaries.

## Extensibility
- **Internal only:** There is no plan for external extensibility. New command types are added as needed for new internal services.
- **Base class interface:** Consumers interact only with base class properties, remaining agnostic to specific subclasses.

## Testability
- **Subsystem unit testing:** Each subsystem is unit tested for its interaction with `Command` instances.
- **Integration by contract:** If all subsystems interact with commands correctly, the system as a whole will integrate correctly.

## Accessibility & Other Considerations
- **Accessibility:** Command metadata (e.g., `Label`, `SpeakPhrase`, `Glyph`) is designed to support accessible UI and speech features.
- **Performance:** No special performance optimizations are required at the data object level.

## Updating This Document
Update this document only when the overall design or boundaries of the `Command` abstraction change, or when new features are added. For implementation details, refer to source code and inline comments.
