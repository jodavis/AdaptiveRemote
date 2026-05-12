# Cloud Assets

Cloud assets are server-fetched, locally-cached data bundles (e.g. the compiled layout) that drive the runtime behavior of the application.

## Composition model

Each asset is a singleton implementing [`ICloudAsset`](ICloudAsset.cs):

| Property | Purpose |
|----------|---------|
| `Name` | Cache key; also used in logging (e.g. `"layout"`) |
| `ResourcePath` | REST path for fetching the latest version |
| `StreamUrl` / `EventName` | SSE endpoint/event for live push notifications (future) |
| `DeserializeAsync` | Converts raw bytes to the asset's runtime type |

Assets are registered via [`CloudAssetServiceExtensions.AddScopedCloudAsset`](../../Configuration/CloudAssetServiceExtensions.cs) and resolved from the DI-scoped [`ICloudAssetStore`](ICloudAssetStore.cs).

## Three-phase orchestrator

[`CloudAssetOrchestrator`](CloudAssetOrchestrator.cs) is a `BackgroundService` and `IPreScopeInitializer`. It runs three phases:

**Phase 1 — cache-first load (blocks scope initialization)**

All assets are loaded in parallel. For each asset:
- If a `.cache` file exists → deserialize from cache; record SHA-256 of those bytes.
- Otherwise → download from server, save to cache, deserialize.

`WaitAsync` returns once all assets are in the store. If any asset fails, `WaitAsync` faults.

**Phase 2 — background server refresh (runs after Phase 1 completes)**

For every asset that was loaded from cache, the server is queried. If the content differs (by SHA-256), the cache and store are updated and an idle-deferred scope recycle is scheduled. Server failures log a warning and are silently skipped.

**Phase 3 — ongoing file-change loop (stub / dev mode)**

Waits on [`IAssetChangeNotifier.WaitForChangeAsync`](IAssetChangeNotifier.cs) in a loop. On each notification, all assets are re-downloaded, cached, and stored, and a recycle is scheduled.

## Cache

[`CloudAssetCache`](CloudAssetCache.cs) reads and writes `.cache` files under `CloudSettings.CachePath` (environment variables expanded). File path: `{CachePath}/{name}.cache`.

## File-change notification

[`FileSystemCloudAssetWatchService`](FileSystemCloudAssetWatchService.cs) watches `CloudSettings.StubFilePath` using `FileSystemWatcher`. It debounces rapid events using a cancel-restart pattern (100 ms delay) and exposes a `SemaphoreSlim(0,1)` so multiple events collapse to a single notification.

This service will be replaced by an SSE-based implementation (ADR-186) without touching the orchestrator.

## Idle-deferred scope recycle

When an update is detected, the orchestrator calls `IdleDeferRecycle`:
- If `IIdleDetector.IsIdle` → `IApplicationRecycleSignal.RequestRecycle()` immediately.
- Otherwise → subscribe to `IIdleDetector.BecameIdle`; recycle when the event fires.

The idle cooldown in tests is set to 0 seconds via `--cloud:IdleCooldownSeconds=0`.

## OAuth2 token provider

[`ICloudAuthTokenProvider`](ICloudAuthTokenProvider.cs) exposes a single method,
`GetTokenAsync(ct)`, that returns a valid Bearer token when configured. If credentials or
endpoint settings are missing, it returns `null` so callers can continue without adding
authorization.

[`CognitoTokenProvider`](CognitoTokenProvider.cs) is the production singleton implementation:
- POSTs to `CloudSettings.CognitoTokenEndpointUrl` using the OAuth2 Client Credentials grant,
  sending `ClientId` and `ClientSecret` from `CloudSettings`.
- Caches the token in memory with its expiry time; serves it on subsequent calls.
- Proactively refreshes when fewer than 60 seconds remain before expiry, making expired tokens
  invisible to callers.
- Uses a `SemaphoreSlim(1,1)` to prevent concurrent fetches (only one refresh in flight at a time).
- Logs a warning and returns `null` (without making an HTTP call) when `ClientId`,
  `ClientSecret`, or `CognitoTokenEndpointUrl` are missing.
- Exceptions propagate to the caller so failures surface in logs at the download layer.

`ClientId`, `ClientSecret`, and `CognitoTokenEndpointUrl` default to empty string. Supply them
via user secrets or environment variables — never commit credentials.

## Adding a new asset type

1. Create a class implementing `ICloudAsset<T>` (or use [`JsonCloudAsset<T>`](JsonCloudAsset.cs)).
2. Register it: `services.AddScopedCloudAsset(new JsonCloudAsset<MyType>(...))`.
3. Inject `MyType` into scoped services via DI — the store resolves it automatically.
