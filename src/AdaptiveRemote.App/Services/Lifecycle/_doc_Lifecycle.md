# Lifecycle Subsystem Architecture & Design

## Overview
The Lifecycle subsystem orchestrates application startup, scope recycling, and shutdown. Its main role is to manage
DI scopes for services that need to be re-initialized when configuration or data changes (e.g., a new compiled layout
downloaded from the backend). It is not responsible for configuration or service orchestration itself; those are
handled by the .NET hosting model.

## Responsibilities & Boundaries
- **Scope management:** Creates and recycles DI scopes for "scoped lifecycle services" when layout updates arrive.
- **Lifecycle hooks:** Calls `InitializeAsync` and `CleanUpAsync` on services implementing [`IScopedLifecycle`](../IScopedLifecycle.cs) at the start and end of each scope.
- **Pre-scope initialization:** Awaits all [`IPreScopeInitializer`](./IPreScopeInitializer.cs) services (e.g., `CloudAssetOrchestrator`) before creating the first scope. Not re-awaited on recycles — the store is already populated.
- **Recycle signaling:** Responds to [`IApplicationRecycleSignal`](./IApplicationRecycleSignal.cs) to trigger a scope recycle; the signal is fired by cloud asset services when a new layout is available.
- **UI independence:** Keeps orchestration logic separate from UI concerns; UI updates are handled via [`ILifecycleViewController`](../ILifecycleViewController.cs) when needed.

## Key Abstractions
- [`IScopedLifecycle`](../IScopedLifecycle.cs): Contract for services that participate in scope lifecycle.
- [`ScopedBackgroundProcess`](../ScopedBackgroundProcess.cs): Base class for background tasks that run within a scope, adapting `IScopedLifecycle` hooks for async method execution.
- [`ILifecycleActivity`](../ILifecycleActivity.cs): Allows services to report progress/status during lifecycle events (useful for UI feedback).
- [`IApplicationScope`/`IApplicationScopeProvider`](../IApplicationScopeFactory.cs): Abstracts DI scope creation, enabling sharing of Blazor-created scopes with other services.
- [`IApplicationRecycleSignal`](./IApplicationRecycleSignal.cs): Cross-service mechanism to request a scope recycle without coupling callers to the scope machinery.
- [`IPreScopeInitializer`](./IPreScopeInitializer.cs): Implemented by singleton services that must fully initialize before the first scope is created.

## Scope provider abstraction
Blazor creates a DI scope for its own components, and that needs to be shared with all the other application services
so that Blazor components can access initialized components. This is handled by `IApplicationScopeProvider`, which
will execute work using a scoped IServiceProvider. The components involved are:
- [`IApplicationScope`](./IApplicationScope.cs): Represents a DI scope in which work can be run.
- [`BlazorAppScope`](../../Components/BlazorAppScope.cs): Implements `IApplicationScope`. Created for the root Blazor component; pushes itself into `IApplicationScopeContainer`. `RecycleAsync()` calls `IJSRuntime.InvokeVoidAsync("location.reload")`, causing the browser to reload and create a new Blazor scope.
- [`IApplicationScopeContainer`](./IApplicationScopeContainer.cs): A scope object (such as `BlazorAppScope`) can be pushed into this interface, which is then used by `IApplicationScopeProvider` as the current scope.
- [`IApplicationScopeProvider`](./IApplicationScopeProvider.cs): Provides the ability to run work items in the current scope, and to recycle (replace) the current scope.
- [`ApplicationLifecycle`](./ApplicationLifecycle.cs): Uses `IApplicationScopeProvider` to get a `ScopedLifecycleContainer`.
- [`ScopedLifecycleContainer`](./ScopedLifecycleContainer.cs): A scoped service that resolves all the `IScopedLifecycle` services and manages calls to `InitializeAsync` and `CleanUpAsync` for the lifetime of the scope.

## Recycle loop
`ApplicationLifecycle.ExecuteAsync` runs as a `while` loop. Each iteration:

1. Creates a linked `CancellationToken` from `stoppingToken + signal.Token`.
2. Calls `TryInitializeScopeAsync`, which invokes `InvokeInScopeAsync` to initialize all scoped services. Returns `true` if initialization completed successfully.
3. If init succeeded, logs `ScopeReady` and waits for either token to fire via `WaitForCancelledAsync` (returns normally — no exception).
4. Runs cleanup unconditionally, then branches:

   **Steady-state recycle** (signal fires after init completes, `stoppingToken` not set):
   cleanup → `RecycleScopeAsync()` (triggers browser reload) → `signal.Reset()` → next iteration.

   **Init-phase recycle** (signal fires while `InitializeAllAsync` is running):
   `InitializeAllAsync` cancels → `TryInitializeScopeAsync` returns `false` → cleanup → `signal.Reset()` → next iteration without a browser reload (the scope TCS is still valid).

   **Shutdown** (stoppingToken fires at any point): break the loop.

   **Init failure** (non-OCE exception during init): cleanup → log `ScopeReleased` → break the loop.

5. After the loop: wait for `stoppingToken` (already cancelled), log `ShuttingDown`, run final cleanup.

The **pre-initializers** (`IPreScopeInitializer`) are only awaited once — before the first scope — and are not re-awaited on recycles, since the asset store is already populated after the first successful scope.

## Recycle signal
[`IApplicationRecycleSignal`](./IApplicationRecycleSignal.cs) / [`ApplicationRecycleSignal`](./ApplicationRecycleSignal.cs):
- `RequestRecycle()`: cancels the internal `CancellationTokenSource`.
- `Token`: the `CancellationToken` linked into the scope work item.
- `Reset()`: disposes the old CTS and creates a fresh one; called by `ApplicationLifecycle` after cleanup, before the next loop iteration.

Callers (cloud asset services) call `RequestRecycle()` without knowing how the recycle is executed.

## Testability
- The subsystem is unit tested using mock `IScopedLifecycle` services to verify correct orchestration and error handling.
- Recycle behavior is tested by injecting a real `ApplicationRecycleSignal` and firing it at specific points.

## Updating This Document
Update this document only when the overall design or boundaries of the Lifecycle subsystem change, or when new features are added. For implementation details, refer to source code and inline comments.
