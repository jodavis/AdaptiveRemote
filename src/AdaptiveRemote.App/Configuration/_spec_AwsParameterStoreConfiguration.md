# AWS Parameter Store Configuration

> **Status:** Draft
> **Will become:** `_doc_AwsParameterStoreConfiguration.md` once implementation is complete

## Overview

AdaptiveRemote fetches sensitive settings (connection strings, API keys) from AWS Systems Manager
Parameter Store at startup. Parameters are stored under a per-environment path
(`/adaptiveremote/{env}/`) and merged into the app's `IConfiguration` pipeline so that existing
settings consumers require no changes. If AWS credentials are not present on the machine, the
Parameter Store source is skipped after logging an information message, and the app starts without
those settings.

## Responsibilities & Boundaries

- **Owns:**
  - Checking whether AWS credentials are available before adding the SSM configuration source
  - Adding SSM Parameter Store as an `IConfigurationSource` during host configuration
  - Logging a startup information message when credentials are absent
  - The `AwsParameterStoreSettings` class that holds connection options (region, path prefix)
  - Expanding `AcceleratedServices` to hold a richer bootstrap `IConfiguration` and a properly
    configured `ILoggerFactory`, both available before the DI host is built
- **Does not own:**
  - Creating or managing parameters in AWS (operational concern)
  - Refreshing parameters at runtime (no reload; restart required). The SSM provider loads all
    parameters into `IConfiguration` once at startup. `IOptionsSnapshot` creates a new snapshot
    per DI scope but reads from the same static `IConfiguration` — it will **not** pull fresh
    values from Parameter Store after startup. A restart is required to pick up parameter changes.
  - Verifying that the resolved credentials have SSM permissions (fails at configuration build time
    if the source is marked optional and permissions are missing; parameters simply won't be loaded)
- **Integrates with:**
  - `AcceleratedServices` — provides the bootstrap logger used to emit the credentials warning
  - `AppHostBuilderExtensions.ConfigureAppSettings` — where the SSM source is added to the pipeline
  - `SettingsKeys` — adds a new key for the AWS settings section
  - `Amazon.Extensions.Configuration.SystemsManager` NuGet package (AWS-maintained)
  - `AWSSDK.Extensions.NETCore.Setup` NuGet package (provides `AWSOptions`)

## Key Design Decisions

### SSM Parameter Store over Secrets Manager

_Context:_ AWS offers both Secrets Manager and Systems Manager Parameter Store for storing
sensitive values.

_Decision:_ Use Parameter Store with `Amazon.Extensions.Configuration.SystemsManager`, the
official AWS-maintained .NET configuration provider.

_Consequences:_ Free standard tier is sufficient for this workload. Path-based naming maps
directly to `IConfiguration` key hierarchy. No official Secrets Manager configuration provider
exists; the available community package adds a dependency risk.

### Separate environment paths

_Context:_ Dev and production parameters must not collide, and it must be possible to run the
app against a different set of secrets depending on the environment.

_Decision:_ The SSM path is `{PathPrefix}{environmentName}/`, where `PathPrefix` defaults to
`/adaptiveremote/` and `environmentName` comes from `IHostEnvironment.EnvironmentName` (i.e., the
`DOTNET_ENVIRONMENT` / `ASPNETCORE_ENVIRONMENT` variable, defaulting to `Production`). Both are
configurable via `IConfiguration`.

_Consequences:_ Parameter names in AWS must include the environment segment
(e.g., `/adaptiveremote/Production/telemetry/connectionString`). Operators must provision
separate parameter sets per environment.

### AWS settings come from IConfiguration

_Context:_ Region and path prefix must be configurable without code changes; they may differ
between developer machines and production.

_Decision:_ `AwsParameterStoreSettings` is bound from the `aws` configuration section, which can
be populated via `appsettings.json`, environment variables, or user secrets. The bootstrap
`IConfiguration` in `AcceleratedServices` (which includes `appsettings.json` and environment
variables) is used to read these settings before the full host is built.

_Consequences:_ The AWS settings are read from the same bootstrap config that feeds the startup
logger, so there is a chicken-and-egg dependency between config and secrets: only settings
available in the bootstrap sources (not SSM itself) can affect how SSM is configured.

### Credential check without an API call

_Context:_ The app must start cleanly and quickly when AWS credentials are absent (e.g., developer machines
without AWS profiles, CI environments). Failing silently without logging is unhelpful; throwing
an exception during startup is unacceptable.

_Decision:_ Before adding the SSM source, attempt to resolve credentials via
`FallbackCredentialsFactory.GetAWSCredentials()`. If this throws `AmazonClientException`, log an
information message via the startup logger and skip the SSM source entirely. If credentials
resolve, add the SSM source with `Optional = true` so that permission errors are non-fatal.

_Consequences:_ The check confirms credential _existence_, not SSM _permissions_. If credentials
exist but lack SSM access, the SSM source will be added, parameters will fail to load silently
(due to `Optional = true`), and no further warning is emitted. This is acceptable; credential
misconfiguration is an operational error, not a startup-safety concern. E2E tests must explicitly
neutralise any ambient AWS credentials (e.g. by clearing `AWS_ACCESS_KEY_ID`,
`AWS_SECRET_ACCESS_KEY`, and `AWS_PROFILE` before launching the host) so that the no-credentials
path is exercised consistently regardless of the developer's local AWS setup.

### ILifecycleActivity progress during SSM loading

_Context:_ SSM parameter loading happens synchronously during `IHostBuilder.Build()`, inside the
`ConfigureAppConfiguration` pipeline. If this call takes several seconds (e.g., on a slow network),
the UI will appear frozen without feedback. The existing `DiagnosticAdapter` solves the same
problem for Azure Key Vault by subscribing to `DiagnosticListener.AllListeners` and intercepting
named SDK diagnostic events.

_Decision:_ Add new event-name handling to `DiagnosticAdapter` for AWS SDK diagnostic events
emitted during SSM parameter loading. `DiagnosticAdapter` already subscribes to
`DiagnosticListener.AllListeners` from construction; no wiring change is needed. When an
SSM-related start event is observed, call `_controller.StartTask("Connecting to AWS Parameter
Store")` to surface progress to the UI. Dispose the activity on the corresponding stop or
exception event. The exact event names must be confirmed during implementation. If the AWS SDK
does not emit compatible `DiagnosticListener` events, defer to future investigation.

_Consequences:_ `DiagnosticAdapter` gains AWS event-name handling alongside its existing Azure Key
Vault logic.

> TBD: Exact diagnostic event names emitted by the AWS SDK during SSM parameter loading must be
> confirmed during implementation.

### Bootstrap IConfiguration expansion in AcceleratedServices

_Context:_ The startup warning must be logged before the DI host is built. The `LoggerFactory`
in `AcceleratedServices` already exists for this purpose, but it is currently configured with a
hardcoded console sink and knows nothing about configured log levels. Similarly, `CommandLineConfig`
only contains command-line arguments, making it impossible to read `appsettings.json` settings
(such as AWS connection options) during startup.

_Decision:_ Replace `CommandLineConfig` with `StartupConfig`, a richer `IConfiguration` built from
(in ascending override order): `appsettings.json`, `appsettings.{env}.json`, environment
variables, and command-line arguments. The environment name is read from the `DOTNET_ENVIRONMENT`
environment variable (matching the .NET hosting default). The `LoggerFactory` is then configured
from `StartupConfig` so that log levels and sinks are respected even during startup, including
the file sink: if `StartupConfig[SettingsKeys.Logging:FilePath]` is set, a `FileLoggerProvider`
is added so that startup log messages (including the SSM credential check result) are captured in
the log file. This is especially important for E2E test runs in CI, where startup failures can
otherwise be silent.

_Consequences:_ `AcceleratedServices` must locate `appsettings.json` relative to
`AppContext.BaseDirectory`. Callers that previously used `CommandLineConfig` must be updated to
use `StartupConfig`. `WpfAcceleratedServices` must drop its own internal `ConfigurationBuilder`
and read `TestingSettings` from `StartupConfig` instead.

### User secrets override SSM

_Context:_ Developers need to override production secrets locally without modifying AWS.

_Decision:_ The SSM source is added to the `IConfigurationBuilder` before user secrets. Because
later sources in the .NET configuration pipeline win, user secrets take precedence over SSM values.

_Consequences:_ The configuration source order in `ConfigureAppSettings` is:
default host sources → SSM → user secrets → command-line args.

## Planned Implementation

### New packages (AdaptiveRemote.App)

- `Amazon.Extensions.Configuration.SystemsManager`
- `AWSSDK.Extensions.NETCore.Setup`

### Interfaces

**`Configuration/IAwsCredentialResolver.cs`**

```csharp
internal interface IAwsCredentialResolver
{
    /// <summary>
    /// Returns true if AWS credentials are available via the default credential chain;
    /// false otherwise.
    /// </summary>
    bool TryResolveCredentials();
}
```

Default implementation `AwsCredentialResolver` calls `FallbackCredentialsFactory.GetAWSCredentials()`
(from `Amazon.Runtime`) and returns `false` on `AmazonClientException`. This interface is the
sole seam for unit-testing the credential-absent branch without hitting the AWS SDK.

### New files

**`Configuration/AwsParameterStoreSettings.cs`**
```csharp
internal class AwsParameterStoreSettings
{
    public string PathPrefix { get; set; } = "/adaptiveremote/";
    public string? Region { get; set; }  // null = use SDK default / environment variable
}
```

**`Configuration/AwsParameterStoreHostBuilderExtensions.cs`**

Internal extension with one public (internal) method:
```csharp
internal static IHostBuilder OptionallyAddAwsParameterStore(
    this IHostBuilder hostBuilder,
    IConfiguration startupConfig,
    ILoggerFactory startupLoggerFactory,
    IAwsCredentialResolver credentialResolver)
```

Behaviour:
1. Read `AwsParameterStoreSettings` from `startupConfig` section `SettingsKeys.Aws`.
2. Call `credentialResolver.TryResolveCredentials()`. If false, log an information message via
   `startupLoggerFactory` (new `MessageLogger` method in the `1100–1199` range — reserve this
   range for `AwsParameterStore`) and return the host builder unchanged.
3. On success, call `hostBuilder.ConfigureAppConfiguration` with a delegate that:
   - Reads `IHostEnvironment.EnvironmentName` from `HostBuilderContext`
   - Builds the SSM path: `settings.PathPrefix + environmentName + "/"`
   - Logs the resolved path at `Debug` level so CI runs confirm which parameters are being loaded
   - Calls `config.AddSystemsManager(...)` with the path, `AWSOptions` (region from settings),
     and `Optional = true`

### Modified files

**`Services/Lifecycle/AcceleratedServices.cs`**

- Add `public string[] Args { get; }` to store the raw command-line arguments for use by the
  host configuration pipeline.
- Replace `CommandLineConfig` with `StartupConfig` of type `IConfigurationRoot`, built from:
  `appsettings.json` (optional) → `appsettings.{env}.json` (optional) →
  environment variables → command-line args
  where `{env}` is read from `Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")`
  (defaulting to `"Production"`).
- Reconfigure `LoggerFactory` creation to use
  `builder.AddConfiguration(StartupConfig.GetSection("Logging"))` in addition to `AddConsole`.
  If `StartupConfig[SettingsKeys.Logging + ":FilePath"]` is non-null, also call
  `builder.AddProvider(new FileLoggerProvider(filePath))` so startup messages are written to the
  log file before the host's own file logging is active.
- Update all internal references from `CommandLineConfig` to `StartupConfig`.

**`Services/Lifecycle/WpfAcceleratedServices.cs`**

- Remove the internal `ConfigurationBuilder` that duplicates command-line parsing.
- Read `TestingSettings` from `base.StartupConfig.GetSection("test").Get<TestingSettings>()`
  instead.

**`AppHostBuilderExtensions.cs`** (`ConfigureAppSettings`)

- Remove the `args` parameter — command-line values are sourced entirely from
  `acceleratedServices.StartupConfig`. Update the signature to:
  ```csharp
  public static IHostBuilder ConfigureAppSettings(
      this IHostBuilder hostBuilder,
      AcceleratedServices acceleratedServices)
  ```
- Add a call to `.OptionallyAddAwsParameterStore(acceleratedServices.StartupConfig,
  acceleratedServices.LoggerFactory, new AwsCredentialResolver())` after the existing sources but
  before `config.AddUserSecrets<UserSecretsKey>()`.
- Update the call site in `AppHostRunner.RunAsync` accordingly.

**`Configuration/SettingsKeys.cs`**

Add:
```csharp
/// <summary>Settings for <see cref="AwsParameterStoreSettings"/>.</summary>
public const string Aws = "aws";
```

**`Services/Lifecycle/DiagnosticAdapter.cs`**

- Add new event-name handling for AWS SDK diagnostic events within the existing
  `DiagnosticListener.AllListeners` subscription (already active from construction — no wiring
  changes needed in `OptionallyAddAwsParameterStore`).
- On an SSM start event, call `_controller.StartTask("Connecting to AWS Parameter Store")` and
  store the returned `ILifecycleActivity`.
- Dispose the activity on the corresponding stop or exception event.
- If the AWS SDK does not emit compatible `DiagnosticListener` events, defer to future
  investigation.

**`Logging/MessageLogger.cs`**

Add two new `[LoggerMessage]` methods (IDs in 1100–1199 range):
```csharp
[LoggerMessage(1100, LogLevel.Information,
    "AWS credentials could not be resolved. Parameter Store configuration will not be loaded.")]
public static partial void AwsCredentialsNotFound(this ILogger logger);

[LoggerMessage(1101, LogLevel.Debug,
    "Loading AWS Parameter Store configuration from path: {Path}")]
public static partial void AwsParameterStorePath(this ILogger logger, string path);
```

### Data Flow

```
AcceleratedServices ctor
  └─ builds StartupConfig (appsettings.json + appsettings.{env}.json + env vars + cli args)
  └─ configures LoggerFactory from StartupConfig["Logging"]
       ├─ AddConsole
       ├─ AddConfiguration(StartupConfig["Logging"])   ← respects configured log levels
       └─ AddProvider(FileLoggerProvider)              ← if FilePath is configured

AppHostRunner.RunAsync
  └─ ConfigureAppSettings(acceleratedServices)
       └─ OptionallyAddAwsParameterStore(startupConfig, loggerFactory, credentialResolver)
            ├─ reads AwsParameterStoreSettings from startupConfig["aws"]
            ├─ credentialResolver.TryResolveCredentials()
            │    ├─ false → log info 1100, return
            │    └─ true → register ConfigureAppConfiguration callback:
            │         ├─ build path = PathPrefix + EnvironmentName + "/"
            │         ├─ log debug 1101 (path)
            │         └─ config.AddSystemsManager(path, Optional=true)
       └─ config.AddUserSecrets<UserSecretsKey>()              ← overrides SSM
       └─ config.AddCommandLine(acceleratedServices.Args)    ← overrides everything

IHostBuilder.Build()
  └─ invokes ConfigureAppConfiguration callbacks (SSM source loads parameters here)
       └─ DiagnosticAdapter (already subscribed to DiagnosticListener.AllListeners) intercepts
          AWS SDK diagnostic events
            ├─ start event → controller.StartTask("Connecting to AWS Parameter Store")
            └─ stop/exception event → activity.Dispose()
```

## Open Questions

- [ ] What diagnostic event names does the AWS SDK (or `Amazon.Extensions.Configuration.SystemsManager`)
      emit via `DiagnosticListener` during SSM parameter loading? Must be confirmed during
      implementation to determine whether `DiagnosticAdapter` event-name handling is viable. If
      no compatible events are emitted, defer the lifecycle progress indicator to a future
      investigation.

## Tasks

### [ADR-156](https://jodasoft.atlassian.net/browse/ADR-156) — Expand AcceleratedServices bootstrap configuration

Replace the narrow `CommandLineConfig` with a full `StartupConfig` that includes `appsettings.json`,
`appsettings.{env}.json`, environment variables, and command-line args. Add `Args` property. Reconfigure
`LoggerFactory` from `StartupConfig` (respecting log levels and file sink). Clean up `WpfAcceleratedServices`
to use `StartupConfig` instead of its own internal `ConfigurationBuilder`.

**Exit criteria:**
- [ ] `AcceleratedServices.StartupConfig` includes values from `appsettings.json`, environment variables,
      and command-line args, with command-line args winning
- [ ] `AcceleratedServices.Args` exposes the raw args array
- [ ] `AcceleratedServices.LoggerFactory` respects log levels configured in `StartupConfig["Logging"]`
- [ ] `AcceleratedServices.LoggerFactory` writes to the file sink when `FilePath` is configured in
      `StartupConfig`
- [ ] `WpfAcceleratedServices` no longer builds its own `ConfigurationBuilder`; reads `TestingSettings`
      from `base.StartupConfig`
- [ ] All existing unit tests pass; new unit tests cover the `StartupConfig` source precedence
- [ ] Headless E2E tests pass
- [ ] `dotnet build /warnaserror` passes with zero warnings

---

### [ADR-157](https://jodasoft.atlassian.net/browse/ADR-157) — AWS Parameter Store configuration provider

Implement `IAwsCredentialResolver`, `AwsParameterStoreSettings`,
`AwsParameterStoreHostBuilderExtensions.OptionallyAddAwsParameterStore`, and wire it into
`ConfigureAppSettings`. Add `SettingsKeys.Aws` and the two `MessageLogger` entries.

**Exit criteria:**
- [ ] `IAwsCredentialResolver` and `AwsCredentialResolver` exist; real implementation calls
      `FallbackCredentialsFactory.GetAWSCredentials()` and returns `false` on `AmazonClientException`
- [ ] `AwsParameterStoreSettings` binds from the `aws` configuration section; `PathPrefix` defaults
      to `/adaptiveremote/`; `Region` defaults to null (SDK default)
- [ ] `OptionallyAddAwsParameterStore` logs message 1100 at `Information` when credentials are absent
      and does not add the SSM source
- [ ] `OptionallyAddAwsParameterStore` logs message 1101 at `Debug` with the resolved path when
      credentials are present and adds the SSM source with `Optional = true`
- [ ] SSM path is constructed as `PathPrefix + IHostEnvironment.EnvironmentName + "/"`
- [ ] User secrets and command-line args override SSM values (source order is preserved)
- [ ] `ConfigureAppSettings` signature takes `AcceleratedServices` instead of `args`; call site in
      `AppHostRunner.RunAsync` is updated
- [ ] Unit tests cover: credentials absent (mocked resolver returns false → no SSM source, message
      1100 logged), credentials present (mocked resolver returns true → SSM source registered, path
      logged)
- [ ] E2E: add an assertion to the existing startup-and-shutdown test that the startup log contains
      message 1100 ("AWS credentials could not be resolved"). AWS credentials are cleared in the
      test environment (see design decision above), so no separate test scenario is needed.
- [ ] `dotnet build /warnaserror` passes with zero warnings

---

### [ADR-160](https://jodasoft.atlassian.net/browse/ADR-160) — AWS Parameter Store operator setup guide

Write the operator runbook that an AWS administrator follows to provision the Parameter Store
parameters consumed by AdaptiveRemote. This task can be started once ADR-157 is complete and
the full list of consumed parameter keys is known.

**Exit criteria:**
- [ ] Runbook document exists (location TBD — ops wiki or checked-in doc)
- [ ] Runbook covers: parameter path convention (`/adaptiveremote/{env}/`), required parameter
      names and types (SecureString vs. String), how to create each parameter via the AWS Console
      or CLI, and what IAM permissions the app's credential identity requires to read them
- [ ] Runbook is reviewed by someone with AWS access

---

### [ADR-158](https://jodasoft.atlassian.net/browse/ADR-158) — DiagnosticAdapter AWS progress indicator

Add AWS SDK event-name handling to `DiagnosticAdapter` so the UI shows "Connecting to AWS Parameter
Store" during SSM parameter loading.

**Exit criteria:**
- [ ] AWS SDK diagnostic event names are identified and documented in a code comment
- [ ] On an SSM start event, `_controller.StartTask("Connecting to AWS Parameter Store")` is called
- [ ] The `ILifecycleActivity` is disposed on the corresponding stop or exception event
- [ ] If no compatible events are found during implementation, the task is closed with a note
      deferring the work and the Open Question in this spec is updated accordingly
- [ ] `dotnet build /warnaserror` passes with zero warnings
- [ ] Headless E2E tests pass; no dedicated test is needed for this UI behaviour as the load
      completes too quickly to observe in a headless run

---

### [ADR-159](https://jodasoft.atlassian.net/browse/ADR-159) — Post-implementation documentation

Replace this spec with `_doc_AwsParameterStoreConfiguration.md`. Remove implementation detail;
keep design intent, key decisions, and links to source.

**Exit criteria:**
- [ ] `_spec_AwsParameterStoreConfiguration.md` is deleted
- [ ] `_doc_AwsParameterStoreConfiguration.md` exists next to the source it describes
- [ ] Doc covers: purpose, key design decisions (SSM over Secrets Manager, environment paths,
      credential check, bootstrap config), and links to key source files
- [ ] No implementation detail (class bodies, method signatures) remains in the doc

## Related Docs

- [`src/AdaptiveRemote.App/Services/_doc_Services.md`](../Services/_doc_Services.md)
- [`src/AdaptiveRemote.App/Services/Lifecycle/_doc_Lifecycle.md`](../Services/Lifecycle/_doc_Lifecycle.md)
- [`src/_doc_Projects.md`](../../_doc_Projects.md)
