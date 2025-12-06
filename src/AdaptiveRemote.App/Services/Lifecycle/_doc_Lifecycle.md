# Lifecycle Subsystem Architecture & Design

## Overview
The Lifecycle subsystem orchestrates application startup, shutdown, and scoped updates. Its main role is to manage DI scopes for services that need to be re-initialized when configuration or data changes. It is not responsible for configuration or service orchestration itself; those are handled by the .NET hosting model.

## Responsibilities & Boundaries
- **Scope management:** Creates and recycles DI scopes for "scoped lifecycle services" when updates occur.
- **Lifecycle hooks:** Calls `InitializeAsync` and `CleanUpAsync` on services implementing [`IScopedLifecycle`](../IScopedLifecycle.cs) at the start and end of each scope.
- **UI independence:** Keeps orchestration logic separate from UI concerns; UI updates are handled via [`ILifecycleViewController`](../ILifecycleViewController.cs) when needed.

## Key Abstractions
- [`IScopedLifecycle`](../IScopedLifecycle.cs): Contract for services that participate in scope lifecycle.
- [`ScopedBackgroundProcess`](../ScopedBackgroundProcess.cs): Base class for background tasks that run within a scope, adapting lifecycle hooks for async method execution.
- [`ILifecycleActivity`](../ILifecycleActivity.cs): Allows services to report progress/status during lifecycle events (useful for UI feedback).
- [`IApplicationScope`/`IApplicationScopeFactory`](../IApplicationScopeFactory.cs): Abstracts DI scope creation, enabling sharing of Blazor-created scopes with other services.

## Design Decisions & Trade-offs
- **Scope factory abstraction:** Required to adapt to Blazor's DI scoping model, keeping the rest of the system UI-agnostic and flexible for future UI changes.
- **Async & testable:** All orchestration is async and designed for unit testability; UI thread dependencies are encapsulated and hidden from most consumers.

## Testability
- The subsystem is unit tested using mock `IScopedLifecycle` services to verify correct orchestration and error handling.

## Future Plans
- The update cycle is not yet implemented, but the architecture is designed to support live updates (e.g., remote layouts, speech models, configuration) so that services can reinitialize with new data without a full restart.

## Updating This Document
Update this document only when the overall design or boundaries of the Lifecycle subsystem change, or when new features are added. For implementation details, refer to source code and inline comments.
