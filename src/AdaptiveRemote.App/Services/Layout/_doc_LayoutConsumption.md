# Client-Side Layout Consumption

## Overview

This feature enables the AdaptiveRemote client to download its compiled remote control layout
from the backend `CompiledLayoutService`, cache it locally, and apply it at startup. It
replaces the static hardcoded `StaticCommandGroupProvider` entirely. When the backend
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
  - `ApplicationScopeContainer` / `IApplicationScopeProvider` — `RecycleScopeAsync` is
    called by `ApplicationLifecycle` to execute a scope recycle
  - `IApplicationRecycleSignal` — new interface in `Services/Lifecycle/`; raised by
    `CloudAssetWatchService` (on SSE-triggered update) and by `CloudAssetOrchestrator` (when
    background server fetch produces a version newer than the cache); awaited by
    `ApplicationLifecycle`
  - `BlazorAppScope.RecycleAsync()` — implemented in this epic; triggers browser reload
    via `IJSRuntime` which creates the next scope
  - `IRemoteDefinitionService` — this epic provides the new implementation
    (`RemoteLayoutDefinitionService`), replacing `StaticCommandGroupProvider`
  - `AdaptiveRemote.Contracts` — `CompiledLayout`, `CommandDefinitionDto`,
    `LayoutGroupDefinitionDto`, `CommandType`
  - Backend spec (ADR-161) — defines the wire format, REST endpoints, and SSE event
    structure consumed here; see
    [`src/_spec_LayoutCustomizationService.md`](../../../_spec_LayoutCustomizationService.md)

## Key Design Decisions

### Replace StaticCommandGroupProvider with RemoteLayoutDefinitionService

_Context:_ Today `StaticCommandGroupProvider` provides a hardcoded layout as the
`IRemoteDefinitionService` implementation. Downloaded layouts must replace it; there is no
value in keeping a fallback static layout.

_Decision:_ Remove `StaticCommandGroupProvider`. Register `RemoteLayoutDefinitionService`
as the sole `IRemoteDefinitionService`. During scope initialization, it reads
`ICloudAssetStore.Get<CompiledLayout>("layout")` — guaranteed populated by
`IPreScopeInitializer` before scope construction — maps the element tree to runtime types,
and appends the GUTTER.

_Consequences:_ All command definitions originate from the backend. First-run requires
backend access. Developers working without the backend must seed the local cache manually.
There is no implicit fallback — if the cache is empty and the backend is unreachable, the
app fails with a fatal error.

### Cache-then-download on startup

_Context:_ The client must be resilient to backend unavailability at startup.

_Decision:_ `CloudAssetOrchestrator` (singleton `BackgroundService`) runs immediately at
application startup, before any scope is created. For each registered `ICloudAsset`, it
first loads from the local cache (`ICloudAssetCache.LoadAsync(asset.Name, ct)`). If a
cached value is found, it calls `asset.ParseAsync(stream, ct)` and stores the result in
`ICloudAssetStore`. Once all assets are loaded from cache, `CloudAssetOrchestrator` signals
`IPreScopeInitializer` complete. If an asset has no cached version, the server fetch must
succeed before signalling; if the server fetch also fails, the app throws a fatal error.
In the background, the orchestrator fetches the latest version from the server for each
asset; if the server version differs from the cached one, it updates the store and schedules
an idle-deferred scope recycle via `IApplicationRecycleSignal`. On scope recycles the store
is already populated; no re-fetch is needed.

_Consequences:_ Users always get the latest layout when the backend is reachable, and can
use the app offline when they have a cached layout. No first-run experience exists without
backend access; this is a known limitation.

### Gutter always appended by the client mapping layer

_Context:_ The GUTTER group (Learn, Exit, ConversationView) is non-optional infrastructure.
Including it in the downloaded layout would let an administrator accidentally remove it.
An alternative — having the backend always inject the GUTTER during compilation — would
allow remote gutter changes without a client update, but any new gutter function would still
require both a recompile of all layouts and a client update, negating the benefit.

_Decision:_ `RemoteLayoutDefinitionService` unconditionally appends a hardcoded GUTTER
`LayoutGroup` containing `LifecycleCommand("Learn")`, `LifecycleCommand("Exit")`, and
`ConversationView()` to the root `LayoutGroup` after mapping the downloaded elements. The
downloaded layout never contains a GUTTER element.

_Consequences:_ Administrators cannot affect gutter behavior or styling. Gutter CSS remains
in `wwwroot` and is unaffected by the compiled layout's `CssDefinitions`.

### CSS injected as inline `<style>` block in Blazor root

_Context:_ `CompiledLayout.CssDefinitions` contains the grid CSS for the downloaded layout
and must replace the `layout.less`-derived portion of the current CSS. Options: write to a
file and configure a WebView2 virtual host mapping (platform-specific); inject inline.

_Decision:_ Inject `CssDefinitions` as a `<style>` block in the Blazor root component via
`IDynamicStylesheetProvider`. No file serving or virtual host mapping is required. The CSS is
available in memory from `ICloudAssetStore` once the scope is initialized.
The `layout.less` grid section is removed from `wwwroot`; only gutter and theme CSS remain.

_Consequences:_ CSS injection is platform-agnostic and contained entirely in
`AdaptiveRemote.App`. CSS is cleanly re-injected on every scope recycle. A compiled layout's
CSS is expected to be small (1–3 KB for a typical button grid); inline injection has no
meaningful performance drawback in this context.

### Layout update deferred until user is idle

_Context:_ Applying a layout update mid-interaction is disruptive and inaccessible,
particularly for eye-gaze users.

_Decision:_ When `CloudAssetWatchService` receives an SSE `layout-ready` event, it
immediately downloads and caches the new layout but defers triggering a scope recycle until
the user is idle. `IIdleDetector` tracks non-idle state via a token pattern:
`StartNonIdle()` returns an `IDisposable`; the system is non-idle while any undisposed
token exists. When the last token is disposed, a cooldown timer starts. After
`CloudSettings.IdleCooldownSeconds` (default: 30 s) with no new `StartNonIdle()` calls,
`IIdleDetector` raises `BecameIdle` and `IsIdle` becomes `true`.

Three scoped adapter services subscribe to ViewModel property changes and call
`StartNonIdle()` / dispose accordingly:
- `ConversationIdleAdapter` — subscribes to `ConversationView.IsListening`
- `ProgrammingModeIdleAdapter` — subscribes to `LifecycleView.IsProgrammingMode`
- `CommandExecutionIdleAdapter` — subscribes to `Command.IsActive` on every command
  provided by `IRemoteDefinitionService`

The adapters are scoped `IScopedLifecycle` services, so they re-subscribe to the new
command set on each scope recycle and release their subscriptions on cleanup.

If a second SSE `layout-ready` event arrives while the first is still waiting for idle,
`CloudAssetWatchService` processes the second download and updates the store, overwriting the
previous value with the newer layout. The existing idle wait covers it — it is waiting for
time elapsed since last non-idle state, not time since the last event — so if the user is
already idle, the update applies immediately.

_Consequences:_ Users are not disrupted by layout updates during active use. Administrators
should expect up to cooldown + current interaction duration before a published layout is
visible on a user's device.

### ApplicationLifecycle owns the full recycle cycle

_Context:_ Currently `ApplicationLifecycle.ExecuteAsync` handles one Initialize/CleanUp
cycle. Supporting scope recycles for layout updates requires it to loop, and the recycle
call itself (`RecycleScopeAsync`) should be owned by `ApplicationLifecycle` so the full
sequence — init → steady state → cleanup → recycle → reinit — is visible and testable in
one place rather than being initiated externally mid-scope.

_Decision:_ Introduce `IApplicationRecycleSignal` in `Services/Lifecycle/`. It exposes a
`CancellationToken Token` that is cancelled when `RequestRecycle()` is called, and a
`Reset()` method that creates a new token (called by `ApplicationLifecycle` after cleanup,
before the next init cycle). `ApplicationLifecycle` creates a linked token from
`stoppingToken + signal.Token` and passes it into `InvokeInScopeAsync`. This means
`RequestRecycle()` cancels the linked token whether it fires during `InitializeAllAsync` or
during the steady-state wait — no special casing required.

Two distinct outcomes follow from where in the cycle the signal fires:

- **Signal during steady state** (init complete, waiting): linked token cancels → scope
  work item returns → `ApplicationLifecycle` calls `CleanUpAllAsync` → calls
  `RecycleScopeAsync` (triggers browser reload) → `Reset()` → loops to await new scope.
- **Signal during init** (e.g. a second `RequestRecycle()` while initializing the new
  scope): linked token cancels `InitializeAllAsync` → scope work item exits early →
  `ApplicationLifecycle` calls `CleanUpAllAsync` on whatever was initialized → `Reset()` →
  loops back to `InvokeInScopeAsync`. No `RecycleScopeAsync` call — the existing scope TCS
  still holds the current scope, so the next `InvokeInScopeAsync` immediately re-enters the
  same scope and retries init.

_Consequences:_ `ApplicationLifecycle` has full visibility into and ownership of the entire
recycle sequence. `CloudAssetWatchService` and any future recycle requestors are decoupled
from the scope machinery — they call `RequestRecycle()` without knowing how it is executed.
The loop is independently testable by firing `IApplicationRecycleSignal` on a mock. A rapid
double-recycle (e.g. two SSE events close together) retries init in the current scope rather
than causing two consecutive browser reloads.

### Startup ordering via IPreScopeInitializer

_Context:_ `CloudAssetOrchestrator` populates `ICloudAssetStore` before scope services are
created. Scoped consumers receive assets via DI factory registrations:

```csharp
services.AddScoped(sp => sp.GetRequiredService<ICloudAssetStore>().Get<CompiledLayout>("layout"));
```

This factory runs at scope construction time — before any `IScopedLifecycle.InitializeAsync`
is called. If scope construction runs before the store is populated, the factory throws.
`IScopedLifecycle` services are initialized in parallel, so a blocking scoped lifecycle
service cannot delay peers.

_Decision:_ Introduce `IPreScopeInitializer` in `Services/Lifecycle/`. `ApplicationLifecycle`
awaits all registered `IPreScopeInitializer` services before calling `InvokeInScopeAsync`
for the first scope. `CloudAssetOrchestrator` implements `IPreScopeInitializer`; its
`WaitAsync` completes once all assets have been loaded from cache (the fast path — background
server fetch continues after signalling). On scope recycles, `IPreScopeInitializer` is not
re-awaited: the store is already populated from the previous cycle.

_Consequences:_ Scoped services that depend on the store are always constructed with a
populated store. The mechanism is generic — any future singleton that must be ready before
scope startup implements `IPreScopeInitializer` without changes to `ApplicationLifecycle`'s
loop logic. The first scope creation is delayed by the time it takes to load the asset cache
(expected to be fast, < 100 ms on typical hardware).

### SSE reconnect with exponential backoff; no user notification on transient disconnect

_Context:_ `CloudAssetWatchService` holds a persistent SSE connection per asset. The
connection will drop on backend restart, network interruption, or extended downtime.
Options were a third-party retry library (e.g. Polly) or a manual reconnect loop using the
built-in `System.Net.ServerSentEvents` parser.

_Decision:_ Use `System.Net.ServerSentEvents` (built into .NET 9+) for SSE parsing.
`CloudAssetWatchService` owns the reconnect loop: on any exception other than
`OperationCanceledException`, wait with exponential backoff (starting at 5 s, doubling to a
cap of 2 min, with jitter), then reconnect silently. No user-visible notification is shown
for transient disconnects — the app continues running on the current layout. Each reconnect
attempt logs a warning; after a configurable number of consecutive failures
(`CloudSettings.SseMaxConsecutiveFailures`, default: 10) an error is logged. No third-party
library is required.

_Consequences:_ No extra dependency. The backoff math is simple (~5 lines). The app is
resilient to backend restarts and brief network interruptions without surfacing noise to the
user. Extended downtime is logged but does not affect app operation; the user's current
layout remains active until connectivity is restored and a `layout-ready` event is received.

### BlazorAppScope.RecycleAsync() uses IJSRuntime to reload the page

_Context:_ `BlazorAppScope.RecycleAsync()` throws `NotImplementedException` today. It needs
a platform-agnostic mechanism to trigger a browser reload.

_Decision:_ `BlazorAppScope.RecycleAsync()` resolves `IJSRuntime` from `_serviceProvider`
and calls `IJSRuntime.InvokeVoidAsync("location.reload")`. This works in both the WPF
BlazorWebView and the Playwright headless host without any platform-specific code or host
registration. No `IWebViewRefresher` interface is needed.

_Consequences:_ The implementation is entirely contained in `AdaptiveRemote.App`. `IJSRuntime`
requires an active Blazor circuit; a call after circuit teardown would throw, but this is
handled by the existing shutdown path in `ApplicationLifecycle`.

### OAuth client credentials sourced from configuration

_Context:_ The client application communicates with the backend using OAuth2 client
credentials flow (as defined in the backend spec, ADR-161). The `client_id` and
`client_secret` must be available on the user's machine without any interactive login.

_Decision:_ `ClientId` and `ClientSecret` are added to `CloudSettings` and sourced from
standard .NET configuration (e.g., environment variables, secrets store in development,
`appsettings.json` for non-secret defaults). A new `ICloudAuthTokenProvider` (singleton)
acquires and caches a bearer token from the Cognito token endpoint using the client
credentials grant, refreshing it before expiry. `CloudAssetDownloader` and
`CloudAssetWatchService` inject `ICloudAuthTokenProvider` and attach the token to all
outgoing requests.

_Consequences:_ No interactive login required. Credentials are never hardcoded. The auth
token lifecycle is centralized in one place and independently testable.

### Cloud assets generalized via untyped ICloudAsset composition

_Context:_ Future assets (speech models, remote settings) will require the same pattern:
load from cache, download from server, SSE-triggered updates, idle-deferred scope recycle.
A typed-plumbing approach (`ICloudAssetStore<T>`, `ICloudAssetCache<T>`,
`ICloudAssetDownloader<T>`) would require one concrete typed implementation per asset. An
untyped composition approach keeps all framework classes non-generic, allows a single
`CloudAssetStore`, `CloudAssetCache`, and `CloudAssetDownloader` to serve all assets, and
makes adding a new asset type purely additive.

_Decision:_ `ICloudAsset` is the per-asset capability bundle providing `Name`, `StreamUrl`,
`EventName`, `ResourcePath`, and `ParseAsync(Stream, CancellationToken): Task<object>`. The
infrastructure singletons (`CloudAssetStore`, `CloudAssetCache`, `CloudAssetDownloader`) are
untyped and keyed by asset name. `CloudAssetOrchestrator` and `CloudAssetWatchService` each
receive `IEnumerable<ICloudAsset>` and loop over all registered assets. Adding a new asset
type requires only a new `ICloudAsset` implementation — typically `JsonCloudAsset<T>`
configured with the appropriate URLs and serializer context — and a DI registration. No
changes to framework classes. The `ICloudAsset<T>` marker interface is provided so
consumers can express type-specific DI registrations. Per-asset typed store/cache/downloader
implementations are eliminated.

_Consequences:_ Framework classes are non-generic and stable. Type safety is enforced at
the `ICloudAssetStore.Get<T>(name)` call site. The `CloudAssets` subsystem is independently
testable via mock `ICloudAsset` implementations.

## Planned Implementation

### New Locations

- `src/AdaptiveRemote.App/Services/CloudAssets/` — shared framework (all asset types)
- `src/AdaptiveRemote.App/Services/Layout/` — layout-specific implementations

### Interfaces

```csharp
namespace AdaptiveRemote.Services.Lifecycle;

// Signals that a scope recycle has been requested. ApplicationLifecycle links this token
// into its scope work item; RequestRecycle() cancels that token whether init is in progress
// or the loop is in steady-state wait. Reset() is called by ApplicationLifecycle after
// cleanup, before starting the next init cycle.
internal interface IApplicationRecycleSignal
{
    void RequestRecycle();
    CancellationToken Token { get; }
    void Reset();
}

// Tracks whether the user is idle. Non-idle state is held open by calling StartNonIdle();
// the returned IDisposable releases the hold when disposed. When all holds are released,
// a cooldown timer starts; BecameIdle fires and IsIdle becomes true after the cooldown.
internal interface IIdleDetector
{
    bool IsIdle { get; }
    event EventHandler BecameIdle;
    IDisposable StartNonIdle();
}

// Implemented by singleton services that must fully initialize before the first scope is
// created. ApplicationLifecycle awaits all registrations before calling InvokeInScopeAsync.
// Not re-awaited on scope recycles.
internal interface IPreScopeInitializer
{
    Task WaitAsync(CancellationToken ct);
}
```

```csharp
namespace AdaptiveRemote.Services.CloudAssets;

// Per-asset capability bundle. One implementation per cloud-fetched asset type.
// Registered as: services.AddSingleton<ICloudAsset>(sp => new JsonCloudAsset<T>(...))
internal interface ICloudAsset
{
    string Name { get; }           // unique key used in store/cache; also used for logging
    string StreamUrl { get; }      // SSE endpoint, e.g. "/notifications/layouts/stream"
    string EventName { get; }      // SSE event name, e.g. "layout-ready"
    string ResourcePath { get; }   // REST base path, e.g. "/layouts/compiled"

    // Parses downloaded or cached bytes into the asset's runtime type.
    Task<object> ParseAsync(Stream stream, CancellationToken ct);
}

// Marker interface — allows type-constrained DI registrations:
//   services.AddScoped(sp => sp.GetRequiredService<ICloudAssetStore>().Get<T>(name))
internal interface ICloudAsset<T> : ICloudAsset { }

// In-memory cross-scope holder for all cloud assets, keyed by Name.
internal interface ICloudAssetStore
{
    T Get<T>(string name);           // Throws if asset not found or wrong type
    void Set(string name, object asset);
}

// File-backed persistence for raw asset bytes, keyed by Name.
internal interface ICloudAssetCache
{
    Task<Stream?> LoadAsync(string name, CancellationToken ct);     // null if no cached file
    Task SaveAsync(string name, Stream assetData, CancellationToken ct);
}

// HTTP download against the backend REST API.
internal interface ICloudAssetDownloader
{
    Task<Stream?> GetActiveAsync(string resourcePath, CancellationToken ct);
    Task<Stream?> GetByIdAsync(string resourcePath, Guid id, CancellationToken ct);
}

// Acquires and caches OAuth bearer tokens via client credentials grant.
// Shared across CloudAssetDownloader and CloudAssetWatchService.
internal interface ICloudAuthTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken ct);
}
```

```csharp
namespace AdaptiveRemote.Services.Layout;

// Scoped. Returns the CSS for the active layout in this scope.
public interface IDynamicStylesheetProvider
{
    string? GetCss();
}
```

### Configuration

```csharp
// Shared connection and auth settings for all cloud asset services.
internal class CloudSettings
{
    public string BackendBaseUrl { get; set; } = "";
    public string CognitoTokenEndpointUrl { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public int IdleCooldownSeconds { get; set; } = 30;
    public int SseMaxConsecutiveFailures { get; set; } = 10;
    public string CachePath { get; set; } = @"%LocalAppData%\AdaptiveRemote\CloudAssets";
}
```

### Key Classes

**`Services/CloudAssets/`**

| Class | Lifetime | Responsibility |
|---|---|---|
| `ApplicationRecycleSignal` | Singleton `IApplicationRecycleSignal` | Backed by a `CancellationTokenSource`; `RequestRecycle()` cancels it; `Reset()` creates a new one |
| `CloudAssetOrchestrator` | Singleton `BackgroundService` + `IPreScopeInitializer` | Loads all `ICloudAsset` registrations from cache in parallel; signals `IPreScopeInitializer` complete once all assets are in the store; then fetches latest from server in background and schedules an idle-deferred recycle if any asset changed |
| `CloudAssetWatchService` | Singleton `BackgroundService` | Opens one SSE connection per `ICloudAsset.StreamUrl`; on matching `EventName`, downloads via `ICloudAssetDownloader.GetActiveAsync`, parses, updates cache and store, then idle-defers `IApplicationRecycleSignal.RequestRecycle()` |
| `IdleDetector` | Singleton `IIdleDetector` | Token-based non-idle tracking; starts cooldown timer when all tokens disposed; raises `BecameIdle` after cooldown |
| `CognitoTokenProvider` | Singleton `ICloudAuthTokenProvider` | POSTs to `CloudSettings.CognitoTokenEndpointUrl`; caches token; refreshes before expiry |
| `BasicCloudAsset<T>` | Abstract `ICloudAsset<T>` | Stores `Name`, `StreamUrl`, `EventName`, `ResourcePath`; `ParseAsync` is abstract |
| `JsonCloudAsset<T>` | Concrete `BasicCloudAsset<T>` | Implements `ParseAsync` using a configurable `JsonSerializerContext`; reusable for any JSON-typed asset |
| `CloudAssetStore` | Singleton `ICloudAssetStore` | Thread-safe in-memory dictionary keyed by asset name |
| `CloudAssetCache` | Singleton `ICloudAssetCache` | Reads/writes raw streams to files under `CloudSettings.CachePath`; creates directory if absent |
| `CloudAssetDownloader` | Singleton `ICloudAssetDownloader` | `HttpClient` wrapper; appends resource path for active/by-id fetches; attaches `Authorization: Bearer` token |

**`Services/Layout/`**

| Class | Lifetime | Responsibility |
|---|---|---|
| `RemoteLayoutDefinitionService` | Scoped `IRemoteDefinitionService` + `IScopedLifecycle` | Calls `ICloudAssetStore.Get<CompiledLayout>("layout")`, maps element tree to runtime types, appends GUTTER; throws descriptive error if store is empty |
| `LayoutStylesheetProvider` | Scoped `IDynamicStylesheetProvider` | Returns `ICloudAssetStore.Get<CompiledLayout>("layout").CssDefinitions` |
| `ConversationIdleAdapter` | Scoped `IScopedLifecycle` | Subscribes to `ConversationView.IsListening`; holds `StartNonIdle()` token while true |
| `ProgrammingModeIdleAdapter` | Scoped `IScopedLifecycle` | Subscribes to `LifecycleView.IsProgrammingMode`; holds `StartNonIdle()` token while true |
| `CommandExecutionIdleAdapter` | Scoped `IScopedLifecycle` | Subscribes to `Command.IsActive` on all commands from `IRemoteDefinitionService`; holds one `StartNonIdle()` token per active command |

The layout `ICloudAsset` registration requires no custom class — `JsonCloudAsset<CompiledLayout>` is
instantiated directly in the DI registration with the layout-specific name, URLs, and
`LayoutContractsJsonContext`:

```csharp
services.AddSingleton<ICloudAsset>(sp => new JsonCloudAsset<CompiledLayout>(
    name: "layout",
    streamUrl: "/notifications/layouts/stream",
    eventName: "layout-ready",
    resourcePath: "/layouts/compiled",
    jsonContext: LayoutContractsJsonContext.Default));

// Direct scoped injection for consumers:
services.AddScoped(sp => sp.GetRequiredService<ICloudAssetStore>().Get<CompiledLayout>("layout"));
```

### DTO → Runtime Type Mapping

`RemoteLayoutDefinitionService` maps `CompiledLayout.Elements` recursively:

| DTO | `CommandType` | Runtime type |
|---|---|---|
| `LayoutGroupDefinitionDto` | — | `LayoutGroup` |
| `CommandDefinitionDto` | `TiVo` | `TiVoCommand` |
| `CommandDefinitionDto` | `IR` | `IRCommand` |
| `CommandDefinitionDto` | `Lifecycle` | `LifecycleCommand` |

After recursive mapping, a hardcoded GUTTER `LayoutGroup` is appended to the root's
children list.

### Data Flow

**Startup:**

1. App starts → singleton `BackgroundService`s start: `CloudAssetOrchestrator`,
   `CloudAssetWatchService`, `ApplicationLifecycle`.
2. `ApplicationLifecycle.ExecuteAsync` begins: awaits all `IPreScopeInitializer.WaitAsync(ct)`
   registrations before proceeding.
3. Concurrently: WPF window opens → Blazor initializes → `BlazorAppScope` pushed into
   `ApplicationScopeContainer`.
4. `CloudAssetOrchestrator` loads from cache in parallel for each `ICloudAsset`:
   `ICloudAssetCache.LoadAsync(asset.Name, ct)` → `asset.ParseAsync(stream, ct)` →
   `ICloudAssetStore.Set(asset.Name, value)`.
5. `CloudAssetOrchestrator` signals `IPreScopeInitializer` complete (all assets in store).
   In the background it also fetches latest from server; if any asset is newer, it updates
   the store and cache and calls `IApplicationRecycleSignal.RequestRecycle()`.
6. `ApplicationLifecycle` unblocks from step 2 → calls `InvokeInScopeAsync`, which awaits
   `BlazorAppScope` (already pushed or waits for it).
7. DI scope is constructed: `sp.GetRequiredService<ICloudAssetStore>().Get<CompiledLayout>("layout")`
   runs successfully because the store is already populated.
8. `ScopedLifecycleContainer.InitializeAllAsync` runs (services initialize in parallel):
   - `RemoteLayoutDefinitionService.InitializeAsync`: calls
     `ICloudAssetStore.Get<CompiledLayout>("layout")`, maps element tree, appends GUTTER,
     sets `RemoteRoot`
   - All other services (`BroadlinkCommandService`, etc.) initialize using
     `IRemoteDefinitionService`
9. App transitions to `LifecyclePhase.Ready`.

**Layout update (normal path — signal fires during steady state):**

1. `CloudAssetWatchService` receives SSE event `layout-ready` on the layout stream.
2. `ICloudAssetDownloader.GetActiveAsync(asset.ResourcePath, ct)` → downloads new version.
3. `asset.ParseAsync(stream, ct)` → `ICloudAssetCache.SaveAsync(asset.Name, stream, ct)` →
   `ICloudAssetStore.Set(asset.Name, value)`.
4. Checks `IIdleDetector.IsIdle`; if not idle, subscribes to `IIdleDetector.BecameIdle`.
5. When idle: calls `IApplicationRecycleSignal.RequestRecycle()` → cancels `signal.Token`.
6. `ApplicationLifecycle`'s linked token cancelled → scope work item exits.
7. `ApplicationLifecycle` calls `CleanUpAllAsync` → calls `_scopeProvider.RecycleScopeAsync()`:
   a. `ApplicationScopeContainer` releases scope (`_scopeTcs` reset)
   b. Calls `BlazorAppScope.RecycleAsync()`
8. `BlazorAppScope.RecycleAsync()` calls `IJSRuntime.InvokeVoidAsync("location.reload")` →
   browser reloads.
9. New Blazor render tree created → new `BlazorAppScope` pushed into `ApplicationScopeContainer`.
10. `ApplicationLifecycle` calls `signal.Reset()`, does **not** re-await `IPreScopeInitializer`,
    then loops back to `InvokeInScopeAsync`. The store already holds the updated layout;
    `RemoteLayoutDefinitionService` reads the new value on next init.

**Layout update (fast path — signal fires during init of next scope):**

If `RequestRecycle()` is called while `InitializeAllAsync` is running (e.g. a new
`layout-ready` arrives before init completes):

1. `signal.Token` cancels → linked token cancels `InitializeAllAsync`.
2. `ApplicationLifecycle` calls `CleanUpAllAsync` on whatever was partially initialized.
3. Does **not** call `RecycleScopeAsync` — the current scope is still valid (browser did not
   reload again).
4. Calls `signal.Reset()`, loops back to `InvokeInScopeAsync`, immediately re-enters the
   same scope, and retries `InitializeAllAsync` with the updated layout.

**CSS injection:**

The Blazor root component (`Remote.razor` or equivalent) injects `IDynamicStylesheetProvider`
and renders:
```html
@if (stylesheet.GetCss() is { } css)
{
    <style>@((MarkupString)css)</style>
}
```
This runs once per scope lifetime; the style is replaced cleanly on every scope recycle.

## Open Questions

- [X] ~~**`CommandType.Action` runtime type:** Resolved: `CommandType.Action` is removed
  from the backend spec. That was an error in that spec.~~

- [X] ~~**SSE reconnect policy:** Resolved: `System.Net.ServerSentEvents` for parsing;
  manual exponential backoff loop (5 s → 2 min cap, jitter) in `CloudAssetWatchService`;
  silent reconnect with warning logging; error logged after `CloudSettings.SseMaxConsecutiveFailures`
  consecutive failures (default 10). No user notification on disconnect.~~

## Tasks

### 1. [ADR-175](https://jodasoft.atlassian.net/browse/ADR-175) — Interfaces, CloudAssetStore, pass-through RemoteLayoutDefinitionService, and stub orchestrator

Replace `StaticCommandGroupProvider` with a minimal store + pass-through definition service
backed by a stub orchestrator that inlines the same hardcoded layout. No DTO mapping or
parsing yet; the store holds runtime types directly.

- [ ] All interfaces defined: `ICloudAsset`, `ICloudAsset<T>`, `ICloudAssetStore`,
  `ICloudAssetCache`, `ICloudAssetDownloader`, `IPreScopeInitializer`
- [ ] `CloudSettings` registered as `IOptions<CloudSettings>`
- [ ] `CloudAssetStore` implemented as thread-safe singleton
- [ ] **Stub `CloudAssetOrchestrator`**: inlines the same hardcoded commands as
  `StaticCommandGroupProvider` (GUTTER included); stores the `LayoutGroup` root directly in
  `CloudAssetStore`; immediately signals `IPreScopeInitializer` complete; no file I/O, no HTTP
- [ ] `StaticCommandGroupProvider` removed
- [ ] `RemoteLayoutDefinitionService` v1: reads `LayoutGroup` directly from
  `ICloudAssetStore.Get<LayoutGroup>("layout")`; returns it as `RemoteRoot`; no DTO mapping
- [ ] `RemoteLayoutDefinitionService` registered as sole `IRemoteDefinitionService`
- [ ] `ApplicationLifecycle` awaits `IPreScopeInitializer` before calling `InvokeInScopeAsync`
  (single-iteration; recycle loop comes in Task 5)
- [ ] Unit tests: `CloudAssetStore.Get<T>` throws on missing key and on wrong type;
  `Set` + `Get<T>` round-trips correctly; `RemoteLayoutDefinitionService` returns store
  contents unchanged; empty store throws with a descriptive message
- [ ] All existing unit and E2E tests pass

---

### 2. [ADR-176](https://jodasoft.atlassian.net/browse/ADR-176) — CSS extraction and stub IDynamicStylesheetProvider

Wire the Blazor root to consume CSS from `IDynamicStylesheetProvider`, backed by a stub
that returns the current static grid CSS extracted from `app.css`.

- [ ] `IDynamicStylesheetProvider` interface defined in `Services/Layout/`
- [ ] Grid CSS extracted from `app.css` into a standalone resource; `LayoutStylesheetProvider`
  v1 returns this content as a static string
- [ ] Blazor root wired to render `<style>@((MarkupString)css)</style>` from
  `IDynamicStylesheetProvider`
- [ ] `app.css` no longer contains the extracted grid CSS
- [ ] Unit tests: `LayoutStylesheetProvider.GetCss()` returns non-null content
- [ ] All existing unit and E2E tests pass

---

### 3. [ADR-177](https://jodasoft.atlassian.net/browse/ADR-177) — CompiledLayout DTO and DTO-to-runtime mapping

Switch the stub orchestrator to push a `CompiledLayout` DTO and implement full DTO-to-runtime
mapping in `RemoteLayoutDefinitionService`.

- [ ] Stub orchestrator updated: constructs a `CompiledLayout` object in code representing
  the same hardcoded layout, without GUTTER (GUTTER is now appended by the definition service)
- [ ] `RemoteLayoutDefinitionService` v2: reads `CompiledLayout` from
  `ICloudAssetStore.Get<CompiledLayout>("layout")`; maps element tree per the DTO mapping
  table; appends GUTTER; throws descriptive error if store is empty
- [ ] DI registration updated:
  `services.AddScoped(sp => sp.GetRequiredService<ICloudAssetStore>().Get<CompiledLayout>("layout"))`
- [ ] Unit tests: each `CommandType` maps to the correct runtime type; GUTTER always appended
  as last root child; unknown `CommandType` throws; empty store throws with descriptive message
- [ ] All existing unit and E2E tests pass

---

### 4. [ADR-178](https://jodasoft.atlassian.net/browse/ADR-178) — JSON parsing, BasicCloudAsset\<T\>/JsonCloudAsset\<T\>, and stub file-based downloader

Introduce the asset abstraction and replace in-code `CompiledLayout` construction with JSON
deserialization from a file, so the stub orchestrator exercises the same parse path as the
real orchestrator will.

- [ ] `BasicCloudAsset<T>` (abstract `ICloudAsset<T>`) and `JsonCloudAsset<T>` (concrete,
  configurable `JsonSerializerContext`) implemented
- [ ] `JsonCloudAsset<CompiledLayout>` registered as `ICloudAsset` with layout name, URLs,
  and `LayoutContractsJsonContext`
- [ ] **Stub `FileCloudAssetDownloader`**: implements `ICloudAssetDownloader`; reads a stream
  from a configured path on disk; `GetByIdAsync` returns null
- [ ] A sample `layout.json` (serialized `CompiledLayout`) checked in for development use
- [ ] Stub orchestrator updated: iterates `IEnumerable<ICloudAsset>`; for each, calls
  `FileCloudAssetDownloader.GetActiveAsync` → `asset.ParseAsync` → `CloudAssetStore.Set`
- [ ] Unit tests: `JsonCloudAsset.ParseAsync` correctly deserializes a `CompiledLayout`;
  `FileCloudAssetDownloader` returns a stream for the configured path; returns null when
  file is absent
- [ ] All existing unit and E2E tests pass

---

### 5. [ADR-179](https://jodasoft.atlassian.net/browse/ADR-179) — ApplicationLifecycle recycle loop and BlazorAppScope.RecycleAsync

Convert `ApplicationLifecycle.ExecuteAsync` to a recycle loop and implement
`BlazorAppScope.RecycleAsync()`.

- [ ] `IApplicationRecycleSignal` and `ApplicationRecycleSignal` added to `Services/Lifecycle/`
- [ ] `ApplicationLifecycle.ExecuteAsync` refactored to `while` loop; linked token from
  `stoppingToken + signal.Token` passed into scope work item
- [ ] `ApplicationLifecycle` awaits all `IPreScopeInitializer.WaitAsync(ct)` before the
  first scope; not re-awaited on subsequent loop iterations
- [ ] Steady-state path: signal fires during wait → cleanup → `RecycleScopeAsync` →
  `signal.Reset()` → loop
- [ ] Init-phase path: signal fires during `InitializeAllAsync` → cancel → cleanup →
  `signal.Reset()` → loop without `RecycleScopeAsync`
- [ ] `BlazorAppScope.RecycleAsync()` implemented via
  `IJSRuntime.InvokeVoidAsync("location.reload")`
- [ ] Unit tests: loop iterates on recycle signal; loop exits cleanly on `stoppingToken`;
  second signal during init cancels init and retries without an additional `RecycleScopeAsync`
  call; first scope creation waits for `IPreScopeInitializer`; recycles do not re-await
  `IPreScopeInitializer`
- [ ] `_doc_Lifecycle.md` updated
- [ ] All existing unit and E2E tests pass

---

### 6. [ADR-180](https://jodasoft.atlassian.net/browse/ADR-180) — Idle detection

Introduce `IIdleDetector`, its implementation, and the three ViewModel adapter services.

- [ ] `IIdleDetector` interface defined in `Services/CloudAssets/`
- [ ] `IdleDetector` implements token-based non-idle tracking with cooldown timer
- [ ] `ConversationIdleAdapter`, `ProgrammingModeIdleAdapter`, `CommandExecutionIdleAdapter`
  implemented as scoped `IScopedLifecycle` services
- [ ] `IdleCooldownSeconds` sourced from `CloudSettings`
- [ ] Unit tests: `IsIdle` is false while any token is held; cooldown starts when last token
  is disposed; `BecameIdle` fires after cooldown; new `StartNonIdle()` during cooldown resets
  the timer; adapters hold/release token in response to ViewModel property changes
- [ ] All existing unit and E2E tests pass

---

### 7. [ADR-181](https://jodasoft.atlassian.net/browse/ADR-181) — Real CloudAssetOrchestrator, CloudAssetCache, FileSystemCloudAssetWatchService, and file-based E2E tests

Replace the stub orchestrator with the real `CloudAssetOrchestrator` backed by the real file
cache and stub file downloader; introduce `FileSystemCloudAssetWatchService` so the full
update path is exercisable and all startup/update scenarios are covered by E2E tests without
any backend dependency.

- [ ] Real `CloudAssetOrchestrator` (singleton `BackgroundService` + `IPreScopeInitializer`):
  loads each `ICloudAsset` from cache in parallel → parse → store → signal complete; in
  background calls `FileCloudAssetDownloader.GetActiveAsync` → updates store/cache →
  `RequestRecycle()` if asset changed
- [ ] `CloudAssetCache` (real file-backed `ICloudAssetCache`): reads/writes streams to files
  under `CloudSettings.CachePath`; creates directory if absent
- [ ] Stub orchestrator removed
- [ ] **`FileSystemCloudAssetWatchService`** (BackgroundService): watches the same configured
  file path as `FileCloudAssetDownloader`; on change, downloads → parses → updates cache and
  store → idle-defers `IApplicationRecycleSignal.RequestRecycle()`; registered in DI in place
  of `CloudAssetWatchService` until Task 12
- [ ] Unit tests: orchestrator signals after cache load; cache miss causes orchestrator to
  wait for downloader; both fail → fatal error; background downloader triggers recycle when
  content differs; `WaitAsync` not re-awaited on recycle path; `CloudAssetCache` writes file
  on `SaveAsync`; returns stream on hit; null on miss; creates directory if absent
- [ ] E2E tests: two test layout fixtures defined in
  `test/AdaptiveRemote.EndToEndTests.TestServices/`:
  — **primary-layout** — same commands as the current static layout (Play, Pause, Exit, TiVo,
  Power); `FileCloudAssetDownloader` is configured to read this fixture by default
  — **updated-layout** — same commands plus `'Guide'` (or another command absent from
  primary-layout, chosen during implementation); the presence of this command is the assertion
  signal for which layout is active in E2E steps

  The E2E test configuration sets `IdleCooldownSeconds: 0` so idle-deferred recycles happen as
  soon as the user is not in an active conversation or command execution.

  Existing steps used without modification:
  `Given the application is not running`,
  `When I start the application`,
  `Then I should see the application in the {LifecyclePhase} phase`,
  `Then I should see the '{string}' button is enabled/disabled`,
  `Then I should not see any error messages in the logs`,
  `Then I should see an error message in the logs:`,
  `When I say {string}`,
  `Then the application should enter listening mode`,
  `Then the application should exit listening mode`

  New steps required — each describes the test environment or what the user observes, not
  application internals:
  - `Given the local layout cache is empty` — deletes the E2E test cache directory before start
  - `Given the local layout cache contains the primary/updated test layout` — pre-seeds the
    cache file from the named fixture
  - `Given the stub layout file is set to the primary/updated test layout` — writes the named
    fixture to the path `FileCloudAssetDownloader` is configured to read
  - `Given the stub layout file is absent` — deletes that file
  - `When I update the stub layout file to the updated test layout` — overwrites the file at
    runtime; `FileSystemCloudAssetWatchService` detects the change and starts the update path
  - `Then I should see a fatal startup error message` — verifies the fatal-error UI is visible

```gherkin
  Scenario: App loads layout from stub file when cache is empty
    Given the application is not running
    And the local layout cache is empty
    And the stub layout file is set to the primary test layout
    When I start the application
    Then I should see the application in the Ready phase
    And I should see the 'Play' button is enabled
    And I should not see any error messages in the logs

  Scenario: App starts from cache when stub file is unchanged
    Given the application is not running
    And the local layout cache contains the primary test layout
    And the stub layout file is set to the primary test layout
    When I start the application
    Then I should see the application in the Ready phase
    And I should see the 'Play' button is enabled
    And I should not see any error messages in the logs

  Scenario: App fails to start when cache is empty and stub file is absent
    Given the application is not running
    And the local layout cache is empty
    And the stub layout file is absent
    When I start the application
    Then I should see a fatal startup error message

  Scenario: App applies updated layout on first idle cycle after startup
    # Cache has primary-layout; stub has updated-layout. Background download detects the
    # difference, triggers a recycle; zero cooldown means it fires immediately on idle.
    Given the application is not running
    And the local layout cache contains the primary test layout
    And the stub layout file is set to the updated test layout
    When I start the application
    Then I should see the application in the Ready phase
    And I should see the 'Guide' button is enabled
    And I should not see any error messages in the logs

  Scenario: App continues on cached layout when background download fails
    # Stub file is absent so the downloader returns null; the app stays on the cached layout.
    Given the application is not running
    And the local layout cache contains the primary test layout
    And the stub layout file is absent
    When I start the application
    Then I should see the application in the Ready phase
    And I should see the 'Play' button is enabled
    And I should see an error message in the logs:
      """
      Failed to download asset 'layout'
      """
    # Note: exact log message text determined in MessageLogger.cs during implementation.

  Scenario: Layout is updated when stub file changes while the user is idle
    Given the application is in the Ready phase
    When I update the stub layout file to the updated test layout
    Then I should see the application in the Ready phase
    And I should see the 'Guide' button is enabled
    And I should not see any error messages in the logs

  Scenario: Layout update is deferred until after an active conversation ends
    Given the application is in the Ready phase
    When I say "Hey Remote"
    Then the application should enter listening mode
    When I update the stub layout file to the updated test layout
    And I say "Thank you"
    Then the application should exit listening mode
    And I should see the application in the Ready phase
    And I should see the 'Guide' button is enabled
    And I should not see any error messages in the logs
```
- [ ] All existing unit and E2E tests pass

---

### 8. [ADR-182](https://jodasoft.atlassian.net/browse/ADR-182) — CSS from CompiledLayout

Switch `LayoutStylesheetProvider` to read `CssDefinitions` from the `CompiledLayout` in the
store; remove the extracted static grid CSS.

- [x] `LayoutStylesheetProvider` v2: returns
  `ICloudAssetStore.Get<CompiledLayout>("layout").CssDefinitions`
  — `LayoutStylesheetProvider` now takes `ICloudAssetStore` as a constructor parameter and
  calls `_store.Get<CompiledLayout>("layout").CssDefinitions` in `GetCss()`. Error handling
  is delegated to `ICloudAssetStore.Get<T>`, which throws `InvalidOperationException` with a
  descriptive message if the asset is not found, matching the pattern used elsewhere.
- [x] Extracted static grid CSS resource removed — `layout-grid.css` deleted;
  `<EmbeddedResource>` entry removed from `AdaptiveRemote.App.csproj`
- [x] Sample `layout.json` fixtures updated to include representative `CssDefinitions` — both
  `primary-layout.json` and `updated-layout.json` in
  `test/AdaptiveRemote.EndtoEndTests.TestServices/Layout/` now contain full grid CSS
  (approx. 1.5 KB) covering `#ROOT`, `#DPAD`, `#WELL`, `#PLAYBACK`, `#CHANNELANDVOLUME`,
  and `#GUTTER` layout rules
- [x] Unit tests: `LayoutStylesheetProvider_GetCss_ReturnsCssFromCompiledLayout` verifies
  return value matches store contents; `LayoutStylesheetProvider_GetCss_EmptyStoreThrowsDescriptiveError`
  verifies that a store exception propagates with a message containing "layout"
- [x] All existing unit and E2E tests pass

---

### 9. [ADR-183](https://jodasoft.atlassian.net/browse/ADR-183) — OAuth token provider

Introduce `ICloudAuthTokenProvider` and `CognitoTokenProvider`.

- [ ] `ICloudAuthTokenProvider` defined in `Services/CloudAssets/`
- [ ] `CognitoTokenProvider` POSTs to `CloudSettings.CognitoTokenEndpointUrl` with
  `client_credentials` grant; caches token; refreshes before expiry
- [ ] `ClientId`, `ClientSecret`, `CognitoTokenEndpointUrl` present in `CloudSettings`
- [ ] Unit tests: token fetched on first call; cached token reused before expiry; token
  refreshed when near expiry; `CancellationToken` respected
- [ ] All existing unit and E2E tests pass

---

### 10. [ADR-184](https://jodasoft.atlassian.net/browse/ADR-184) — E2E test infrastructure — Docker Compose for backend services

Set up Docker Compose so the real backend services are available before HTTP implementations
are introduced in subsequent tasks.

- [ ] `docker-compose.yml` starts `CompiledLayoutService` and `NotificationService` with
  test configuration
- [ ] Both services expose test-only control endpoints when `ASPNETCORE_ENVIRONMENT=Test`
  (to be added to ADR-161 backend spec); these are the seams that make Tasks 11–12 E2E
  scenarios exercisable without manual intervention:
  - `POST /test/layouts/set-active` — activates a named test layout fixture on the backend
  - `POST /test/backend/set-unavailable` / `POST /test/backend/set-available` — makes all
    requests return 503 or restores normal operation
  - `POST /test/backend/set-download-unavailable` — makes only the download endpoint return 503
    while SSE continues to function
  - `POST /test/sse/publish-event` — stores a new active layout version and sends a
    `layout-ready` SSE event to all connected clients
  - `POST /test/sse/disconnect-all` — forcibly closes all active SSE connections
- [ ] New step definitions added to `AdaptiveRemote.EndToEndTests.Steps` for the
  backend-control steps used in Tasks 11–12
- [ ] E2E test fixture starts and stops the compose stack as part of test setup/teardown
- [ ] Tests wait for service health checks before proceeding
- [ ] `test/_doc_EndToEndTests.md` updated to document Docker-based E2E setup

---

### 11. [ADR-185](https://jodasoft.atlassian.net/browse/ADR-185) — Real CloudAssetDownloader

Replace `FileCloudAssetDownloader` with the real HTTP `CloudAssetDownloader`; startup
scenarios now run against the real backend.

- [ ] `CloudAssetDownloader` wraps `HttpClient`; appends resource path for active/by-id
  fetches; attaches `Authorization: Bearer` token via `ICloudAuthTokenProvider`
- [ ] `FileCloudAssetDownloader` removed from DI
- [ ] Unit tests: `Authorization` header attached on every request; `GetActiveAsync`
  constructs the correct URL; null returned on 404; exception propagated on other HTTP errors
- [ ] E2E tests: startup scenarios repeated against the Docker backend using the test-only
  control endpoints from Task 10. Reuses the primary/updated test layout fixtures and all
  step definitions from Task 7.

  New backend-control steps (calling the test-only API from Task 10):
  - `Given the backend has the primary/updated test layout active` — calls
    `POST /test/layouts/set-active`
  - `Given the backend is not responding` — calls `POST /test/backend/set-unavailable`

```gherkin
  Scenario: App loads layout from backend when cache is empty
    Given the application is not running
    And the local layout cache is empty
    And the backend has the primary test layout active
    When I start the application
    Then I should see the application in the Ready phase
    And I should see the 'Play' button is enabled
    And I should not see any error messages in the logs

  Scenario: App falls back to cache when backend is unavailable at startup
    Given the application is not running
    And the local layout cache contains the primary test layout
    And the backend is not responding
    When I start the application
    Then I should see the application in the Ready phase
    And I should see the 'Play' button is enabled
    And I should not see any error messages in the logs

  Scenario: App fails to start when backend is unavailable and cache is empty
    Given the application is not running
    And the local layout cache is empty
    And the backend is not responding
    When I start the application
    Then I should see a fatal startup error message

  Scenario: App applies updated layout on first idle cycle when backend has newer version
    Given the application is not running
    And the local layout cache contains the primary test layout
    And the backend has the updated test layout active
    When I start the application
    Then I should see the application in the Ready phase
    And I should see the 'Guide' button is enabled
    And I should not see any error messages in the logs
```
- [ ] All existing unit and E2E tests pass

---

### 12. [ADR-186](https://jodasoft.atlassian.net/browse/ADR-186) — Real CloudAssetWatchService — SSE subscription and update flow

Replace `FileSystemCloudAssetWatchService` with the real SSE-based `CloudAssetWatchService`;
SSE update scenarios now run against the real backend.

- [ ] `CloudAssetWatchService` opens one SSE connection per `ICloudAsset.StreamUrl` using
  `System.Net.ServerSentEvents`
- [ ] On event matching `asset.EventName`: calls
  `ICloudAssetDownloader.GetActiveAsync(asset.ResourcePath, ct)`, `asset.ParseAsync`,
  `ICloudAssetCache.SaveAsync(asset.Name, ...)`, `ICloudAssetStore.Set(asset.Name, ...)`,
  then idle-defers `IApplicationRecycleSignal.RequestRecycle()`
- [ ] Exponential backoff reconnect loop (5 s → 2 min cap, jitter) on any non-cancellation
  exception
- [ ] Warning logged per reconnect attempt; error logged after
  `CloudSettings.SseMaxConsecutiveFailures` consecutive failures
- [ ] Bearer token attached via `ICloudAuthTokenProvider`
- [ ] `FileSystemCloudAssetWatchService` removed from DI
- [ ] Unit tests: store and cache updated on matching event; non-matching events ignored;
  `RequestRecycle()` called after idle; backoff delay increases on repeated failures; second
  SSE event overwrites store with newer value without duplicate recycle request
- [ ] E2E tests: SSE-triggered update scenarios run against the Docker backend using the
  test-only control endpoints from Task 10. Reuses primary/updated test layout fixtures and
  all step definitions from Tasks 7 and 11.

  New backend-control steps (calling the test-only API from Task 10):
  - `When the backend publishes the updated test layout` — calls `POST /test/sse/publish-event`,
    which stores the updated layout as active and sends a `layout-ready` SSE event to all
    connected clients
  - `When the backend drops all SSE connections` — calls `POST /test/sse/disconnect-all`
  - `Given the backend download endpoint is not responding` — calls
    `POST /test/backend/set-download-unavailable`
  - `When the backend sends a layout-ready SSE event` — calls `POST /test/sse/publish-event`
    with the current active layout unchanged (notifies without providing new content)

```gherkin
  Scenario: Layout is updated when backend publishes while user is idle
    Given the application is in the Ready phase
    When the backend publishes the updated test layout
    Then I should see the application in the Ready phase
    And I should see the 'Guide' button is enabled
    And I should not see any error messages in the logs

  Scenario: Layout update is deferred until after an active conversation ends
    Given the application is in the Ready phase
    When I say "Hey Remote"
    Then the application should enter listening mode
    When the backend publishes the updated test layout
    And I say "Thank you"
    Then the application should exit listening mode
    And I should see the application in the Ready phase
    And I should see the 'Guide' button is enabled
    And I should not see any error messages in the logs

  Scenario: App continues running when the SSE connection is dropped
    Given the application is in the Ready phase
    When the backend drops all SSE connections
    Then I should see the application in the Ready phase
    And I should not see any error messages in the logs

  Scenario: App continues on current layout when download fails after SSE notification
    Given the application is in the Ready phase
    And the backend download endpoint is not responding
    When the backend sends a layout-ready SSE event
    Then I should see the application in the Ready phase
    And I should see the 'Play' button is enabled
    And I should see an error message in the logs:
      """
      Failed to download asset 'layout'
      """
    # Note: exact log message text determined in MessageLogger.cs during implementation.
```
- [ ] All existing unit and E2E tests pass

---

## Related Docs

- [`src/AdaptiveRemote.App/Services/Lifecycle/_doc_Lifecycle.md`](../Lifecycle/_doc_Lifecycle.md)
- [`src/AdaptiveRemote.App/Services/Commands/_doc_Commands.md`](../Commands/_doc_Commands.md)
- [`src/AdaptiveRemote.App/Components/_doc_UI.md`](../../Components/_doc_UI.md)
- [`src/AdaptiveRemote.App/Services/ProgrammaticSettings/_doc_ProgrammaticSettings.md`](../ProgrammaticSettings/_doc_ProgrammaticSettings.md)
- [`src/_doc_Projects.md`](../../../_doc_Projects.md)
- [`src/_spec_LayoutCustomizationService.md`](../../../_spec_LayoutCustomizationService.md) — backend spec (ADR-161); defines `CompiledLayout` wire format, REST endpoints, and SSE event types
