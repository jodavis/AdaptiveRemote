# Test Logging Design

Goal
- Provide a logging channel for end-to-end test code so tests can write structured logs that are recorded by the application's `ILogger` pipeline and are also visible inside the test runner (`MSTest.TestContext`).

Requirements
- Tests should use an API consistent with the project's `ILogger` semantics and message templates.
- Test-originated log messages must be recorded by the application host `ILogger` pipeline (so they flow to the same sinks as host logs).
- Test-originated log messages must also be visible in the test runner (`TestContext`).
- Tests must be able to create nested scopes.
- Keep RPC surface and serialization simple: support structured templates with primitive args only.
- Support dynamic registration/unregistration of sinks so host RPC sinks can attach/detach when connections open/close.
- Preserve ordering across sinks by waiting for remote sinks to acknowledge receipt of log and scope operations.

Design summary
- Define a single sink API `ITestLogger` (marshalable over JSON-RPC) that represents a log sink. All sinks (host-side RPC sink, local TestContext sink, and any others) implement `ITestLogger`.
- Provide a test-process intermediary that implements the standard .NET logging primitives by exposing an `ILoggerProvider` (`HostApplicationLoggerProvider`). This provider creates category `ILogger` instances that forward log operations to the intermediary.
- The intermediary maintains a dynamic collection of registered `ITestLogger` sinks. When a sink is registered the intermediary will forward subsequent `BeginScope`/`Log` calls to every registered sink.
- The host will expose an `ITestLogger` RPC target instance for each JSON-RPC connection. When a test host connection is established the test host client will register that RPC-backed `ITestLogger` with the intermediary; when the connection closes it will unregister it.
- The intermediary also registers a local `ITestLogger` that writes formatted lines to the MSTest `TestContext` so test output shows messages immediately.

Where files live
- Host RPC interface and host sink implementation:
  - `src/AdaptiveRemote.App/Services/Testing/ITestLogger.cs` (RPC-marshalable API)
  - `test/AdaptiveRemote.EndtoEndTests.TestServices/Logging/HostApplicationTestLogger.cs` (host-side implementation that logs into host `ILogger` and returns an async-disposable scope)
- Test-side intermediary and sink implementations:
  - `test/AdaptiveRemote.EndtoEndTests.TestServices/Logging/HostApplicationLoggerProvider.cs` (implements `ILoggerProvider` and manages registered `ITestLogger` proxy)
  - `test/AdaptiveRemote.EndtoEndTests.TestServices/Logging/TestContextLoggerProvider.cs` (local sink that writes to `TestContext`)

API shape (single source of truth)
- [`ITestLogger`](../../../src/AdaptiveRemote.App/Services/Testing/ITestLogger.cs) (marshalable over JSON-RPC) — sinks implement this API and the intermediary dispatches to the registered sinks.

```csharp
[RpcMarshalable]
public interface ITestLogger : IDisposable
{
    Task LogMessageAsync(int logLevel, string category, int eventId, string? eventName, string message, CancellationToken cancellationToken);
    Task<IAsyncDisposable> BeginScopeAsync(string category, string scopeName, CancellationToken cancellationToken);
}
```

Intermediary behavior and wiring
- Test process provides `HostApplicationLoggerProvider : ILoggerProvider` that is registered in test DI. `ILogger<T>` resolved in test-side services comes from this provider.
- `HostApplicationLoggerProvider` creates `HostRpcLogger` instances per category. Each `HostRpcLogger` implements `ILogger` and forwards calls to the provider which performs dispatch to the registered `ITestLogger` proxy when attached.
- The provider exposes a thread-safe `AttachTestLoggerProxy(ITestLogger)` method so a proxy sink can be registered when it becomes available (e.g., on host connection).
- When a proxy is attached the provider will synchronously call remote `BeginScopeAsync`/`LogMessageAsync` operations and wait for them to complete to preserve ordering across sinks. Sinks are invoked in the order they are attached.

Dispatch semantics
- For each log or scope operation the provider currently waits synchronously for the registered `ITestLogger` proxy to acknowledge the operation. This ensures deterministic ordering between local and host logs but means sinks must respond quickly.
- Consequence: sinks must be fast and non-blocking. The test could be delayed by logging calls if a sink is slow or blocked. Consider using explicit synchronization patterns in tests if necessary.

Scope handling
- Test-side `ILogger.BeginScope` will call the remote `ITestLogger.BeginScopeAsync` (if a remote proxy is attached). The provider waits for the remote call to return an `IAsyncDisposable` and returns an `IDisposable` that will call `DisposeAsync()` on the remote scope when disposed.
- Host RPC sink maintains any per-connection scope context it requires (server-side). The host implementation `HostApplicationTestLogger` returns an `IAsyncDisposable` that wraps `ILogger.BeginScope(...)`, so host sinks see the scope context as normal .NET logging scopes.
- Local TestContext sink records the scope name as part of the formatted output.

Host-side details
- The host creates instances of `HostApplicationTestLogger` by using `TestEndpointService.CreateTestLoggerAsync(...)` to load the test-side logger type into the application scope and return it as an RPC target to the test process.
- `HostApplicationTestLogger` translates incoming `LogMessageAsync` calls into `ILogger.Log` invocations on the host application logger and returns remote scope objects from `BeginScopeAsync` so the test-side provider can propagate scope boundaries.

TestContext sink
- `TestContextLoggerProvider` implements a local sink that writes formatted messages and scope begin/end lines to `TestContext.WriteLine(...)`.
- The intermediary can register this sink for tests that have a `TestContext` instance available (e.g., test base sets it up at test start).

CLI and durable host logging
- The host app (`AdaptiveRemote.App`) supports `--test:LogFile=<path>` to enable a file sink in addition to console. End-to-end tests should pass this flag via `AdaptiveRemoteHostSettings.AddCommandLineArgs("--test:LogFile=path")` so a durable copy of the host logs is captured.
- Host file sink should append and flush frequently so logs are available even if the host is killed.

Serialization constraints and limitations
- Only primitives are supported for `args`; structured objects must be flattened to primitives (or `ToString()`) before being sent.
- Exceptions are transported as text only.

Backward-compatibility and incremental rollout
- Implement `ITestLogger` and the provider/sink scaffolding first with minimal functionality:
  1. Host `HostApplicationTestLogger` that logs to host `ILogger` and returns an async-disposable scope.
  2. Test `HostApplicationLoggerProvider` + test-side proxy attach + `TestContextLoggerProvider`.
- Wire host connection code to create the RPC target and ensure the test client registers/unregisters the RPC proxy with the provider on connect/disconnect.
- Improve formatting and robust error handling later if needed.

Open questions resolved
- Dynamic sink registration: yes (attach on connect / unregister on disconnect).
- Use .NET logging primitives: yes — implement an `ILoggerProvider` in the test process to avoid duplicating .NET logging semantics.
- Dispatch semantics: provider currently waits for remote sinks to acknowledge operations to preserve ordering.
