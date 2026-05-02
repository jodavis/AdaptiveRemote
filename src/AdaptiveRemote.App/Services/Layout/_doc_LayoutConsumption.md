# Client-Side Layout Consumption

## Overview

AdaptiveRemote downloads its compiled remote control layout from the backend
`CompiledLayoutService`, caches it locally, and applies it at startup. When the backend
publishes a new layout, the client downloads it and automatically applies it the next time
the user is idle. The downloaded `CompiledLayout` (defined in `AdaptiveRemote.Contracts`)
is mapped to the runtime `Command` and `LayoutGroup` types used by all other subsystems.
CSS from the compiled layout is injected into the Blazor page as a `<style>` block at scope
initialization time and refreshed on every scope recycle.

## Responsibilities & Boundaries

- **Owns:** Asset download from backend, local file cache, DTO-to-runtime mapping,
  gutter append, CSS injection via inline `<style>`, SSE subscription, idle detection,
  idle-deferred scope recycle trigger, OAuth token acquisition for backend API and SSE requests
- **Does not own:** Layout compilation or storage (backend, ADR-161); backend SSE server
- **Integrates with:**
  - `ApplicationLifecycle` — becomes a recycle loop; awaits all `IPreScopeInitializer`
    services (including `CloudAssetOrchestrator`) before creating the first scope
  - `IApplicationRecycleSignal` — raised by `CloudAssetWatchService` (on SSE-triggered
    update) and by `CloudAssetOrchestrator` (when background server fetch produces a version
    newer than the cache); awaited by `ApplicationLifecycle`
  - `IRemoteDefinitionService` — `RemoteLayoutDefinitionService` replaces
    `StaticCommandGroupProvider` as the sole implementation
  - `AdaptiveRemote.Contracts` — `CompiledLayout`, `CommandDefinitionDto`,
    `LayoutGroupDefinitionDto`, `CommandType`
  - Backend spec (ADR-161) — defines the wire format, REST endpoints, and SSE event
    structure consumed here; see
    [`src/_spec_LayoutCustomizationService.md`](../../../_spec_LayoutCustomizationService.md)

## Key Design Decisions

### No fallback static layout

There is no value in keeping a fallback static layout alongside the downloaded one.
`RemoteLayoutDefinitionService` is the sole `IRemoteDefinitionService`. If the local cache
is empty and the backend is unreachable at startup, the app fails with a fatal error rather
than silently providing a stale or mismatched layout. Developers without backend access must
seed the local cache manually.

### Cache-then-download on startup

`CloudAssetOrchestrator` loads each registered `ICloudAsset` from the local file cache
first. If a cached value exists, the store is populated and `IPreScopeInitializer` signals
complete before any scope is constructed. The background server fetch runs in parallel; if
the server version differs from the cached one, the store and cache are updated and a
scope recycle is scheduled. This makes the app resilient to backend unavailability at
startup without delaying first scope creation beyond cache load time (expected < 100 ms).

### Gutter always appended by the client mapping layer

The GUTTER group (Learn, Exit, ConversationView) is non-optional infrastructure. Including
it in the downloaded layout would let an administrator accidentally remove it.
`RemoteLayoutDefinitionService` unconditionally appends a hardcoded GUTTER `LayoutGroup`
after mapping the downloaded elements. The downloaded layout never contains a GUTTER element.
Gutter CSS remains in `wwwroot` and is unaffected by the compiled layout's `CssDefinitions`.

### CSS injected as inline `<style>` block

`CompiledLayout.CssDefinitions` contains the grid CSS for the downloaded layout elements
(`#DPAD`, `#WELL`, `#PLAYBACK`, `#CHANNELANDVOLUME`). Structural layout rules (`#ROOT`, `#GUTTER`)
are static and live in `wwwroot/css/app.less` rather than in `CssDefinitions`.
`LayoutStylesheetProvider` is a scoped service that receives `CompiledLayout` by direct
constructor injection (possible because `CompiledLayout` is itself registered as scoped via
`AddScopedCloudAsset`). The Blazor root component injects the CSS as an inline `<style>`
block via `IDynamicStylesheetProvider`. This avoids file serving or WebView2 virtual host
mapping (both platform-specific). CSS is cleanly re-injected on every scope recycle.

### Layout update deferred until user is idle

Applying a layout update mid-interaction is disruptive and inaccessible, particularly for
eye-gaze users. When `CloudAssetWatchService` receives an SSE `layout-ready` event, it
immediately downloads and caches the new layout but defers triggering a scope recycle until
`IIdleDetector` reports the user is idle (after a configurable cooldown, default 30 s).
Three scoped adapter services (`ConversationIdleAdapter`, `ProgrammingModeIdleAdapter`,
`CommandExecutionIdleAdapter`) hold non-idle tokens while the user is actively interacting.

### ApplicationLifecycle owns the full recycle cycle

`ApplicationLifecycle.ExecuteAsync` runs as a loop. It creates a linked
`CancellationToken` from `stoppingToken + IApplicationRecycleSignal.Token` and passes it
into the scope work item. When `RequestRecycle()` is called, the linked token cancels
whether init is in progress or the loop is in steady-state — no special casing required.
After cleanup, `ApplicationLifecycle` calls `RecycleScopeAsync` (which triggers a browser
reload via `IJSRuntime`) if the signal fired during steady state; if it fired during init,
the loop retries init in the current scope without a reload. `IPreScopeInitializer` is
awaited only before the first scope; subsequent recycles skip it because the store is
already populated.

### Cloud assets generalized via untyped ICloudAsset composition

Infrastructure singletons (`CloudAssetStore`, `CloudAssetCache`, `CloudAssetDownloader`)
are untyped and keyed by asset name. Each asset type provides a single `ICloudAsset`
implementation (typically `JsonCloudAsset<T>`) that owns its name, endpoints, and
deserialization. Adding a new asset type requires only a new `ICloudAsset` implementation
and a DI registration — no changes to framework classes. Type safety is enforced at the
`ICloudAssetStore.Get<T>(name)` call site.

## Source

- [`Services/Layout/`](.) — `LayoutStylesheetProvider`, `RemoteLayoutDefinitionService`, idle adapters
- [`Services/CloudAssets/`](../CloudAssets/) — `CloudAssetStore`, `CloudAssetOrchestrator`, `CloudAssetWatchService`, `IdleDetector`
- [`Services/Lifecycle/`](../Lifecycle/) — `ApplicationRecycleSignal`, `IPreScopeInitializer`

## Related Docs

- [`src/AdaptiveRemote.App/Services/Lifecycle/_doc_Lifecycle.md`](../Lifecycle/_doc_Lifecycle.md)
- [`src/AdaptiveRemote.App/Services/Commands/_doc_Commands.md`](../Commands/_doc_Commands.md)
- [`src/AdaptiveRemote.App/Components/_doc_UI.md`](../../Components/_doc_UI.md)
- [`src/_doc_Projects.md`](../../../_doc_Projects.md)
- [`src/_spec_LayoutCustomizationService.md`](../../../_spec_LayoutCustomizationService.md) — backend spec (ADR-161)
