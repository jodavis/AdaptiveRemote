# Services Folder Architecture & Design

## Overview
The top-level `Services` folder organizes all public API interfaces for services, along with helpers and subsystem implementations. This structure enables clear separation between subsystem consumers and implementation details.

## Responsibilities & Boundaries
- **Public API interfaces:** Top-level `Services` folder contains interfaces for communication between subsystems, available without including subsystem namespaces.
- **Subsystem implementations:** Placed in subfolders with separate namespaces to avoid leaking implementation details into the public API surface.
- **Helpers:** Base classes and extension methods with common functionality are included at the top level.

## Key Design Decisions
- **Naming conventions:** Interfaces start with `I`; async methods end with `Async` (standard .NET conventions).
- **Extension methods:** Should be defined in a static class named `<Interface>Extensions.cs`, targeting the interface used as the `this` parameter.
- **Dependency injection:** Uses .NET Host for DI and configuration. Services that depend on configuration data should be scoped; others should be singletons.
- **Coupling:** Helpers and extension methods must only depend on public service interfaces, not on internal implementations. If functionality depends on implementation details, it should be part of the service interface.

## Usage Patterns
- **Extension methods:** Implement additional functionality that can be satisfied by other parts of the API (e.g., recursive directory creation for `IFileSystem`). New implementations of an interface do not need to reimplement logic provided by extensions.

## Updating This Document
Update this document only when the overall design or boundaries of the Services folder change, or when new patterns or conventions are introduced. For implementation details, refer to source code and inline comments.
