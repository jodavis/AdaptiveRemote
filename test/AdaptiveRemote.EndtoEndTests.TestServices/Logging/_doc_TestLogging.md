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
- Dispatch to sinks in a non-blocking, fire-and-forget fashion to avoid slowing tests.

Design summary
- Define a single sink API `ITestLogger` (marshalable over JSON-RPC) that represents a log sink. All sinks (host-side RPC sink, local TestContext sink, and any others) implement `ITestLogger`.
- Provide a test-process intermediary that implements the standard .NET logging primitives by exposing an `ILoggerProvider` ("TestLoggingProvider"). This provider creates category `ILogger` instances that forward log operations to the intermediary.
- The intermediary maintains a dynamic collection of registered `ITestLogger` sinks. When a sink is registered the intermediary will forward subsequent `BeginScope`/`EndScope`/`Log` calls to every registered sink.
- The host will expose an `ITestLogger` RPC target instance for each JSON-RPC connection. When a test host connection is established the test host client will register that RPC-backed `ITestLogger` with the intermediary; when the connection closes it will unregister it.
- The intermediary also registers a local `ITestLogger` that writes formatted lines to the MSTest `TestContext` so test output shows messages immediately.

Where files live
- Host RPC interface and host sink implementation:
  - `src/AdaptiveRemote.App/Services/Testing/ITestLogger.cs` (RPC-marshalable API)
  - `test/AdaptiveRemote.EndToEndTests.TestServices/HostTestLogger.cs` (host-side implementation that logs into host `ILogger` and maintains per-connection scope stack)
- Test-side intermediary and sink implementations:
  - `test/AdaptiveRemote.EndtoEndTests.TestServices/TestLoggerProvider.cs` (implements `ILoggerProvider` and manages registered `ITestLogger` sinks)
  - `test/AdaptiveRemote.EndtoEndTests.TestServices/TestContextTestLogger.cs` (local sink that writes to `TestContext`)

API shape (single source of truth)
- `ITestLogger` (marshalable over JSON-RPC) — sinks implement this API and the intermediary dispatches to the registered sinks.

```csharp
[RpcMarshalable]
public interface ITestLogger
{
    Task PushScopeAsync(string scopeName);
    Task PopScopeAsync();
    Task LogAsync(
        LogLevel level,
        int eventId,
        string? eventName,
        string messageTemplate,
        object?[] args,
        string? exceptionText,
        string? category);
}
```

- Notes:
  - `args` are limited to primitive values (string, numeric, bool). Non-primitives must be converted to strings before being sent.
  - `exceptionText` is `exception.ToString()` produced on the caller side.
  <!-- How is exceptionText going to be used on the host side? LogError takes an Exception object, not a string -->
  - `eventName` mirrors `EventId.Name` and is optional.

Intermediary behavior and wiring
- Test process provides `TestLoggingProvider : ILoggerProvider` that is registered in test DI. `ILogger<T>` resolved in test-side services comes from this provider.
- `TestLoggingProvider` creates `TestLogger` instances per category. Each `TestLogger` implements `ILogger` and forwards calls to the provider which performs dispatch to registered `ITestLogger` sinks.
- The provider exposes thread-safe `AddSink(ITestLogger)` and `RemoveSink(ITestLogger)` methods so sinks can be registered dynamically.
- When a sink is registered it will immediately start receiving subsequent `BeginScope`/`EndScope`/`Log` calls. Sinks are invoked in the order they are registered.

Dispatch semantics
- For each log or scope operation the provider will fire-and-forget a dispatch to each registered `ITestLogger` sink. The provider will not await the sink calls.
- Rationale: avoid blocking test threads on network/RPC latency. Tests must not rely on synchronous delivery timing. If tests need to validate that logs arrived at a remote sink they should use explicit synchronization checks in test code (e.g., host-side wait utilities).
- Consequence: ordering across sinks is best-effort and not strictly guaranteed under concurrency or RPC delays.

Scope handling
- Test-side `ILogger.BeginScope` returns an `IDisposable` that calls the provider to dispatch `PushScopeAsync(scopeName)` to all sinks and returns a disposable that on Dispose dispatches `PopScopeAsync()` to all sinks.
- Host RPC sink maintains a per-connection scope stack (server-side). `PushScopeAsync` pushes a scope, `PopScopeAsync` pops it. Because the host sink is per-connection, `LogAsync` calls do not need to carry a scope id.
- Local TestContext sink simply records the scope name as part of the formatted output.

Host-side details
- The host will implement `TestLoggerRpcTarget` and register it as a local RPC target on each `JsonRpc` instance for a test control connection.
- `TestLoggerRpcTarget` translates incoming `LogAsync` calls into `ILogger.Log` invocations on the host application logger. It should include the active per-connection scopes (via `ILogger.BeginScope`) so messages retain scope context in host sinks.
- `TestControlService` (or the code that Accepts the TcpClient/JsonRpc) should create the `TestLoggerRpcTarget` instance and add it to the `JsonRpc` so the test client can attach to it. When a connection is detected on the test-process side, the test client constructs an `RpcTestLoggerSink` with the RPC proxy and registers it with the intermediary provider. On disconnect the intermediary removes the sink.

TestContextTestSink
- A local `TestContextTestSink` implements `ITestLogger` and writes formatted messages to MSTest `TestContext.WriteLine(...)`.
- The intermediary registers this sink automatically for tests that have a `TestContext` instance available (e.g., test base sets it up at test start).

CLI and durable host logging
- The host app (`AdaptiveRemote.App`) supports `--test:LogFile=<path>` to enable a file sink in addition to console. End-to-end tests should pass this flag via `AdaptiveRemoteHostSettings.AddCommandLineArgs("--test:LogFile=path")` so a durable copy of the host logs is captured.
- Host file sink should append and flush frequently so logs are available even if the host is killed.

Serialization constraints and limitations
- Only primitives are supported for `args`; structured objects must be flattened to primitives (or `ToString()`) before being sent.
- Exceptions are transported as text only.
- Because sink dispatch is fire-and-forget, tests must not assume immediate delivery of messages to remote sinks; use explicit synchronization where required.

Backward-compatibility and incremental rollout
- Implement `ITestLogger` and the provider/sink scaffolding first with minimal functionality:
  1. Host `TestLoggerRpcTarget` that logs to host `ILogger` and tracks scopes.
  2. Test `TestLoggingProvider` + `RpcTestLoggerSink` + `TestContext` sink.
- Wire host connection code to create the RPC target and ensure the test client registers/unregisters the RPC sink with the provider on connect/disconnect.
- Improve formatting, robust error handling, and optional host->test forwarding later if needed.

Open questions resolved
- Dynamic sink registration: yes (register on connect / unregister on disconnect).
- Use .NET logging primitives: yes — implement an `ILoggerProvider` in the test process to avoid duplicating .NET logging semantics.
- Dispatch semantics: fire-and-forget (non-blocking).
