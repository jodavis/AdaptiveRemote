# Lifecycle Subsystem Architecture & Design

Summary: Describes DI scope management, lifecycle hooks, and the Blazor scope-sharing model used for service initialization and teardown.

## Overview
The Lifecycle subsystem orchestrates application startup, shutdown, and scoped updates. Its main role is to manage
DI scopes for services that need to be re-initialized when configuration or data changes. It is not responsible for
configuration or service orchestration itself; those are handled by the .NET hosting model.

## Responsibilities & Boundaries
- **Scope management:** Creates and recycles DI scopes for "scoped lifecycle services" when updates occur.
- **Lifecycle hooks:** Calls `InitializeAsync` and `CleanUpAsync` on services implementing [`IScopedLifecycle`](../IScopedLifecycle.cs) at the start and end of each scope.
- **UI independence:** Keeps orchestration logic separate from UI concerns; UI updates are handled via [`ILifecycleViewController`](../ILifecycleViewController.cs) when needed.

## Key Abstractions
- [`IScopedLifecycle`](../IScopedLifecycle.cs): Contract for services that participate in scope lifecycle.
- [`ScopedBackgroundProcess`](../ScopedBackgroundProcess.cs): Base class for background tasks that run within a scope, adapting `IScopedLifecycle` hooks for async method execution.
- [`ILifecycleActivity`](../ILifecycleActivity.cs): Allows services to report progress/status during lifecycle events (useful for UI feedback).
- [`IApplicationScope`/`IApplicationScopeProvider`](../IApplicationScopeFactory.cs): Abstracts DI scope creation, enabling sharing of Blazor-created scopes with other services.

## Scope provider abstraction
Blazor creates a DI scope for its own components, and that needs to be shared with all the other application services
so that Blazor components can access initialized components. This is handled by `IApplicationScopeProvider`, which
will execute work using a scoped IServiceProvider. The components involved are:
- [`IApplicationScope`](./IApplicationScope.cs): Represents a DI scope in which work can be run.
- [`BlazorAppScope`](../../Components/BlazorAppScope.cs): Implements `IApplicationScope`. This object is created for the root Blazor component, and pushes itself into the `IApplicationScopeContainer`.
- [`IApplicationScopeContainer`](./IApplicationScopeContainer.cs): A scope object (such as `BlazorAppScope`) can be pushed into this interface, which is then used by `IApplicationScopeProvider` as the current scope.
- [`IApplicationScopeProvider`](./IApplicationScopeProvider.cs): Provides the ability to run work items in the current scope.
- [`ApplicationLifecycle`](./ApplicationLifecycle.cs): Uses `IApplicationScopeProvider` to get a `ScopedServiceContainer`.
- [`ScopedLifecycleContainer`](./ScopedLifecycleContainer.cs): A scoped service that resolves all the `IScopedLifecycle` services and manages calls to `InitializeAsync` and `CleanUpAsync` for the lifetime of the scope. 

## Testability
- The subsystem is unit tested using mock `IScopedLifecycle` services to verify correct orchestration and error handling.

## Future Plans
- The update cycle is not yet implemented, but the architecture is designed to support live updates (e.g., remote layouts, speech models, configuration) so that services can reinitialize with new data without a full restart.

## Updating This Document
Update this document only when the overall design or boundaries of the Lifecycle subsystem change, or when new features are added. For implementation details, refer to source code and inline comments.
