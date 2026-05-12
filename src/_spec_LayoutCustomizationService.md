# Layout Customization Service (Backend)

> **Status:** Draft
> **Will become:** `_doc_LayoutCustomizationService.md` once implementation is complete

## Overview

The Layout Customization Service is a microservice backend that enables administrators to
remotely create, edit, compile, and publish remote control layouts for the AdaptiveRemote
client application. Administrators edit layouts via a web editor; the backend stores,
compiles, validates, and distributes them; client applications download and cache layouts
and update automatically when new versions are published. This epic covers the backend
services only. The web editor UI, client-side layout integration, CI/CD deployment, and
load testing are covered by separate related epics.

## Terminology

| Term | Meaning |
|------|---------|
| Client application | The end-user AdaptiveRemote Windows app (remote control) |
| Editor application | The administrator-facing Blazor WebAssembly app for editing layouts |
| Backend | The microservices defined in this epic |
| End user | Person using the client application |
| Administrator | Person using the editor application to modify layouts for an end user |
| Raw layout | An administrator-editable layout definition (source format, JSON) |
| Compiled layout | A processed layout ready for client consumption (command JSON + CSS) |

## Responsibilities & Boundaries

- **Owns:** Storage, compilation, validation, and distribution of remote layouts via REST
  APIs; layout change notifications to connected clients via SSE
- **Does not own:** Web editor UI; client-side layout consumption, caching, and update
  application; CI/CD deployment pipeline; load testing infrastructure; user authentication
  (delegated to external IdP); layout schema definition (defined in the editor epic)
- **Integrates with:** External OAuth2 identity provider (JWT validation); `AdaptiveRemote.Contracts`
  shared library (layout DTOs); client application (SSE consumer); editor application
  (layout CRUD consumer)

## Key Design Decisions

### Repo organization: solution filters, not folder split

_Context:_ Backend projects will be added to the same repo as the client application. Options
were to reorganize into top-level `client/` and `backend/` folders, or to keep everything
under `src/` and `test/` and use solution filters.

_Decision:_ Keep all projects under `src/` and `test/`. Add `client.slnf` and `backend.slnf`
solution filters so developers can load only the relevant set. A master `AdaptiveRemote.sln`
includes all projects.

_Consequences:_ No folder restructuring of existing client projects. Consistent layout for
both audiences. Backend project names follow the convention `AdaptiveRemote.Backend.*` to
avoid collision with client projects.

### DynamoDB for layout storage; SQS for processing queue

_Context:_ Layout access patterns are almost entirely key/user/timestamp lookups with opaque
JSON content. A relational database would add server management overhead without providing
relational features we'd actually use. Given the AWS deployment target, purpose-built managed
AWS services are the natural fit.

_Decision:_ Use **DynamoDB** for `RawLayoutService` and `CompiledLayoutService` storage.
Partition key is `UserId`; sort key is `Id` (a KSUID or similar time-ordered ID). This
covers all access patterns: point-read by ID, list all layouts for a user, get the active
layout by user. Layout elements (`RawLayout.Elements`, `CompiledLayout.Elements`) and CSS
(`CompiledLayout.CssDefinitions`) are serialized to JSON strings and stored as DynamoDB
string attributes. Use **SQS** as the message queue between `RawLayoutService` and
`LayoutProcessingService`. The SQS queue is configured with a **max receive count of 3**
(4 total attempts including the first); messages that exhaust retries are moved to a
**Dead Letter Queue (DLQ)**. DLQ messages are retained for 14 days. `LayoutProcessingService`
logs an error on every failed attempt and on DLQ arrival. The raw layout's `ValidationResult`
is not automatically updated for DLQ messages; manual reprocessing (by re-saving the raw
layout) is required. This is a known limitation and a candidate for future improvement.

_Consequences:_ No database server to provision or manage in production. Pay-per-request
pricing is well-suited to low initial traffic. Local development uses LocalStack
(a Docker container that emulates DynamoDB, SQS, and Lambda). Strong .NET support via AWSSDK.
The DynamoDB single-table design requires upfront key schema decisions; the partition/sort key
model above is sufficient for all current access patterns. Adding a new query pattern (e.g.,
list layouts by name) may require a Global Secondary Index.

### Direct HTTP between services for MVP; event-driven boundary preserved

_Context:_ The epic raised event-driven architecture (e.g., Kafka) as a question. Event-driven
adds substantial infrastructure complexity (broker, consumer groups, at-least-once delivery)
not justified at MVP scale, but the architecture should not rule it out.

_Decision:_ Services communicate via direct HTTP for the initial implementation, with one
exception: `RawLayoutService` enqueues a message to SQS when a layout is saved, and
`LayoutProcessingService` polls that queue. This makes compilation inherently asynchronous —
the editor receives a `201 Created` immediately after save and learns that compilation is
complete via an SSE event (the same stream the client uses). All other service-to-service
calls (e.g., `LayoutProcessingService` → `LayoutCompilerService`) remain synchronous HTTP.
Each service-to-service communication boundary is modeled as an injected interface so the
transport can be changed without modifying callers.

_Consequences:_ Async processing prevents slow compilation from blocking the editor. The
SQS queue provides natural retry and backpressure if `LayoutProcessingService` is
temporarily unavailable. Synchronous HTTP for the remaining internal calls keeps the design
simple for MVP.

### All service-to-service communication is interface-abstracted

_Context:_ The transport mechanism for any given service-to-service call may need to change
(e.g., HTTP → SQS, or direct call → fan-out to multiple consumers). Callers should not be
coupled to transport details.

_Decision:_ Every cross-service call is expressed as an injected interface in the calling
service. The interface captures intent (what is being requested or notified), not transport.
Implementations are registered in DI and can be swapped without changing callers. This
applies uniformly: storage repositories, HTTP client wrappers, SQS publishers, and SSE
notification publishers all follow this pattern.

_Consequences:_ Transport changes are contained to the implementation class and DI
registration. The pattern adds a small amount of indirection but makes each service
independently testable with mock implementations. No special framework is required —
standard .NET DI is sufficient.

### Service discovery and load balancing

_Context:_ Services that communicate via HTTP need a way to locate each other. The approach
differs between local development and production.

_Decision:_

**Production:** Services run as Docker containers on **AWS ECS (Fargate)**. Internal
service-to-service traffic uses **ECS Service Connect**, which provides DNS-based service
discovery and client-side load balancing within the ECS cluster. Each service registers
under a short name (e.g., `rawlayoutservice`); callers reach it at
`http://rawlayoutservice/...` with no additional infrastructure. External traffic from the
client and editor applications enters through **AWS API Gateway**, which handles auth
validation and routes requests to the appropriate ECS service.

**Local development:** Docker Compose provides service discovery automatically via Docker
DNS. Services are reachable by their Compose service name (e.g.,
`http://rawlayoutservice:8080`). No additional tooling is required.

**Configuration:** Each service's base URL is injected via environment variable or
`appsettings.json`. No URLs are hardcoded. The same binaries run locally and in production;
only configuration changes.

_Consequences:_ ECS Service Connect eliminates the need for a separate internal load
balancer or service mesh. Docker Compose DNS makes local development zero-configuration.
The environment-variable–driven URL model is a standard .NET pattern and requires no
framework changes.

### Orchestration over choreography for the compilation pipeline

_Context:_ `LayoutProcessingService` coordinates five steps (fetch → compile → validate →
store → notify) and is therefore coupled to five other services. Choreography was considered
as an alternative: each service would react to events rather than being called, eliminating
the central coordinator.

_Decision:_ Keep the orchestrator pattern. The compilation pipeline is strictly linear with
no fan-out, making choreography's main benefit (independent step scaling and reuse across
workflows) inapplicable. In an orchestrated design, error handling — specifically the
`ValidationResult` write-back to `RawLayoutService` on failure — lives in one place with
full context. In a choreographed design, `RawLayoutService` would need to subscribe to
failure events, adding business logic to a CRUD service. The orchestrator's coupling is
managed through injected interfaces (independently testable) and it owns no storage of its
own. Revisit choreography if the pipeline grows significantly or steps need to be reused
across multiple workflows.

_Consequences:_ The overall workflow is explicit and debuggable in one place. `LayoutProcessingService`
is intentionally coupled to its participants — this is the orchestrator pattern working as
designed, not a design flaw.

### Lambda for stateless services; ECS Fargate for stateful services

_Context:_ `LayoutCompilerService` and `LayoutValidationService` are stateless and invoked
only when an administrator saves a layout — not on the hot path of any client request.
Running them as always-on ECS containers means paying for idle capacity on services that
may go hours without being invoked.

_Decision:_ Host `LayoutCompilerService` and `LayoutValidationService` as **AWS Lambda
functions**. All other services run as **ECS Fargate** containers. Lambda functions are
exposed via **Lambda Function URLs** (no API Gateway layer needed for internal calls);
`LayoutProcessingService` reaches them over HTTPS using the existing `ILayoutCompilerClient`
and `ILayoutValidationClient` HTTP interfaces — the ECS-to-Lambda boundary is transparent
to callers. Use **Native AOT** compilation for the Lambda functions to minimize cold start
latency. Cold starts are acceptable regardless, because `LayoutProcessingService` is already
running asynchronously via SQS — a cold start adds seconds to a background process, not to
a user-facing response. LocalStack emulates Lambda locally, consistent with the existing
DynamoDB and SQS setup.

_Consequences:_ Pay-per-invocation cost model for low-frequency services. No idle container
cost. Native AOT requires that Lambda function code avoids reflection-heavy libraries.
Lambda Function URLs keep the calling convention identical to ECS HTTP services, preserving
the interface abstraction.

### Shared contracts library for layout definition DTOs; existing App types stay in App

_Context:_ The client application, editor application, and backend all need to work with
layout data structures. The existing `RemoteLayoutElement` and `Command` types in
`AdaptiveRemote.App.Models` were considered for sharing, but they inherit from `MvvmObject`
and carry MVVM properties, execution delegates, and client lifecycle concerns — they cannot
live in a framework-agnostic library.

_Decision:_ Introduce `AdaptiveRemote.Contracts` as a shared .NET class library (no
**platform-specific** dependencies, no `-windows` target) containing layout definition DTOs
and a source-generated `JsonSerializerContext`. "No platform-specific dependencies" means
no WPF, Windows APIs, or Blazor — BCL libraries including `System.Text.Json` and
`System.Collections.Generic` are permitted and expected. The library contains pure records
representing what a layout element *is* — name, label, glyph, grid position, CSS overrides
— with no behavior. The existing `Command` and `RemoteLayoutElement` types remain in
`AdaptiveRemote.App` as runtime types; they are mapped from the Contracts DTOs at
layout-apply time (responsibility of the client-side consumption epic).

`AdaptiveRemote.Contracts` defines a `LayoutContractsJsonContext : JsonSerializerContext`
annotated with `[JsonSerializable]` for each top-level DTO type. This serves two purposes:
source-generated serialization is **required** for the Native AOT Lambda functions
(`LayoutCompilerService`, `LayoutValidationService`), and placing the context in Contracts
ensures all consumers share one consistent serialization definition rather than maintaining
separate contexts that could drift.

The client application uses the Contracts DTOs and context directly for deserializing API
responses. JSON field names and structure are defined once and shared by both the
serializing backend and the deserializing client.

`AdaptiveRemote.Contracts` is included in both `client.slnf` and `backend.slnf`.

_Consequences:_ Single source of truth for the wire format. Breaking changes to shared
types are caught at compile time across all consumers. The App runtime types and Contracts
DTOs are not duplicates — they serve different purposes (runtime behavior vs. data
transport). The mapping from DTO to runtime type is a contained, testable step.

### Server-Sent Events for client push notifications

_Context:_ The `NotificationService` needs to push layout-change events to connected clients.
WebSockets support bidirectional communication, which is unnecessary — clients only need to
receive events.

_Decision:_ Use Server-Sent Events (SSE) over HTTPS. The client application opens a
persistent SSE connection on startup. Standard SSE retry handles reconnection automatically.

_Consequences:_ Simpler server implementation than WebSockets. Works through most HTTP
proxies and firewalls. Limitation: SSE is one-way; if bidirectional communication is
needed in the future, migration to WebSockets would be required.

### OAuth2 with AWS Cognito; two flows for two client types

_Context:_ The client application runs unattended on a disabled user's machine and cannot
present an interactive login. Stress bot accounts need to be provisioned programmatically
without manual IdP UI work. A custom API key store was considered but would require owning
key generation, hashing, rotation, and revocation — a non-trivial security surface.

_Decision:_ Use **AWS Cognito** as the identity provider with two OAuth2 flows:

- **Authorization Code flow** — for administrators using the editor application. Standard
  browser-based login; Cognito handles MFA, session management, and token refresh.
- **Client Credentials flow** — for the client application and stress bot accounts. Each
  machine client is registered as a Cognito app client with a `client_id` and
  `client_secret`, stored in environment variables or a config file. Tokens are acquired
  and refreshed automatically in the background; no user interaction occurs. Bot accounts
  are provisioned and revoked via the Cognito API (scriptable, no manual console work).

All services validate JWT bearer tokens from Cognito using the published JWKS endpoint.
Services receive the `sub` claim as the stable user identifier. No custom auth service or
user database is required.

For local development, use a **dedicated Cognito dev user pool** rather than a local OIDC
stub. This avoids incomplete emulation and ensures auth behavior matches production exactly.
The dev user pool requires only AWS credentials and internet access — both already assumed
for LocalStack configuration.

_Consequences:_ Client application and bot auth is non-interactive and config-file–driven,
matching the desired UX. Cognito handles all security-sensitive concerns (key storage, token
signing, revocation). Cognito is AWS-native, consistent with DynamoDB, SQS, and Lambda.
The dev user pool adds a small AWS dependency to local development but is free within
Cognito's free tier.

### Auto-update layout on notification; defer application until user is idle

_Context:_ When the backend publishes a new compiled layout, the client needs to update.
Applying immediately risks disrupting an active interaction; requiring a manual user action
adds friction.

_Decision:_ Auto-update. When the client receives an SSE layout-changed event, it fetches
the new compiled layout. It defers applying it (swapping the active layout) until the user
is idle. The exact idle-detection policy is defined in the client-side consumption epic.

_Consequences:_ End users always see the latest layout without manual intervention. The
deferral policy protects against jarring mid-interaction updates but is out of scope for
this epic.

## Planned Implementation

### Project naming convention

| Project | Type |
|---------|------|
| `AdaptiveRemote.Contracts` | Shared class library (DTOs, enums) |
| `AdaptiveRemote.Backend.RawLayoutService` | .NET 10 Web API — ECS Fargate |
| `AdaptiveRemote.Backend.CompiledLayoutService` | .NET 10 Web API — ECS Fargate |
| `AdaptiveRemote.Backend.LayoutCompilerService` | .NET 10 Lambda function (Native AOT) |
| `AdaptiveRemote.Backend.LayoutValidationService` | .NET 10 Lambda function (Native AOT) |
| `AdaptiveRemote.Backend.LayoutProcessingService` | .NET 10 Web API — ECS Fargate |
| `AdaptiveRemote.Backend.NotificationService` | .NET 10 Web API — ECS Fargate (SSE) |

Test projects follow the pattern `<ProjectName>.Tests` under `test/`.

### Shared Contracts (`AdaptiveRemote.Contracts`)

```csharp
// Identifies the runtime command type. The client uses this to instantiate the correct
// App runtime type (TiVoCommand, IRCommand, LifecycleCommand, ActionCommand).
// Type-specific execution parameters are resolved by the client from its own configuration:
//   TiVo   — CommandId = Name.ToUpperInvariant() (existing convention)
//   IR     — payload programmed via remote, stored in ProgrammaticSettings
//   Others — keyed by Name
// Subtypes with additional properties are deferred until a concrete need arises.
public enum CommandType { Lifecycle, TiVo, IR }

// Shared behavioral interface — prevents drift between the compiled and raw command types.
// Adding a new behavioral property means updating this interface first; the compiler
// will flag any implementing record that doesn't follow.
public interface ICommandProperties
{
    CommandType Type { get; }
    string Name { get; }
    string Label { get; }
    string? Glyph { get; }
    string SpeakPhrase { get; }
    string? Reverse { get; }
}

// ---------------------------------------------------------------------------
// Compiled layout element DTOs
// Used in CompiledLayout.Elements. Deserialized directly by the client application.
// Contains only behavioral properties — grid positions and CSS overrides have been
// compiled into CssDefinitions and are not needed by the client.
// ---------------------------------------------------------------------------

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CommandDefinitionDto), "command")]
[JsonDerivedType(typeof(LayoutGroupDefinitionDto), "group")]
public abstract record LayoutElementDto(string CssId);

// Maps to AdaptiveRemote.App.Models.Command at layout-apply time (client epic).
// Type carries the CommandType discriminator so the client knows which runtime type to instantiate.
// No subtype hierarchy is used — all behavioral properties are flat; type-specific execution
// parameters are resolved by the client from its own configuration (see CommandType above).
public record CommandDefinitionDto(
    CommandType Type,
    string Name,
    string Label,
    string? Glyph,
    string SpeakPhrase,
    string? Reverse,
    string CssId
) : LayoutElementDto(CssId), ICommandProperties;

// Maps to AdaptiveRemote.App.Models.LayoutGroup at layout-apply time (client epic).
public record LayoutGroupDefinitionDto(
    string CssId,
    IReadOnlyList<LayoutElementDto> Children
) : LayoutElementDto(CssId);

// ---------------------------------------------------------------------------
// Raw layout element DTOs
// Shared between the editor application (serialization) and LayoutCompilerService
// (deserialization). Extends behavioral properties with authoring properties that
// the compiler resolves into CssDefinitions and strips from the compiled output.
// ---------------------------------------------------------------------------

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RawCommandDefinitionDto), "command")]
[JsonDerivedType(typeof(RawLayoutGroupDefinitionDto), "group")]
public abstract record RawLayoutElementDto(
    string CssId,
    int GridRow,
    int GridColumn,
    int GridRowSpan = 1,
    int GridColumnSpan = 1,
    string? AdditionalCss = null    // per-element CSS overrides (e.g. red background for Power)
);

public record RawCommandDefinitionDto(
    CommandType Type,
    string Name,
    string Label,
    string? Glyph,
    string SpeakPhrase,
    string? Reverse,
    string CssId,
    int GridRow,
    int GridColumn,
    int GridRowSpan = 1,
    int GridColumnSpan = 1,
    string? AdditionalCss = null
) : RawLayoutElementDto(CssId, GridRow, GridColumn, GridRowSpan, GridColumnSpan, AdditionalCss),
    ICommandProperties;

public record RawLayoutGroupDefinitionDto(
    string CssId,
    IReadOnlyList<RawLayoutElementDto> Children,
    int GridRow,
    int GridColumn,
    int GridRowSpan = 1,
    int GridColumnSpan = 1,
    string? AdditionalCss = null
) : RawLayoutElementDto(CssId, GridRow, GridColumn, GridRowSpan, GridColumnSpan, AdditionalCss);

// ---------------------------------------------------------------------------
// Top-level layout records
// ---------------------------------------------------------------------------

// Administrator-editable source format. Elements are typed; no opaque JSON string.
public record RawLayout(
    Guid Id,
    string UserId,
    string Name,
    IReadOnlyList<RawLayoutElementDto> Elements,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ValidationResult? ValidationResult    // written by LayoutProcessingService via IRawLayoutStatusWriter
);

// Client-consumable format produced by LayoutCompilerService.
// Deserialized directly by the client application — no intermediate parsing model needed.
// The client maps Elements → runtime Command objects at layout-apply time (client epic).
public record CompiledLayout(
    Guid Id,
    Guid RawLayoutId,
    string UserId,
    bool IsActive,
    int Version,
    IReadOnlyList<LayoutElementDto> Elements,
    string CssDefinitions,                // global CSS for the layout grid
    DateTimeOffset CompiledAt
);

// Editor-consumable preview format, produced by LayoutCompilerService.
public record PreviewLayout(
    Guid RawLayoutId,
    int Version,
    string RenderedHtml,
    string RenderedCss,
    DateTimeOffset CompiledAt,
    ValidationResult ValidationResult
);

public record ValidationIssue(string Code, string Message, string? Path);
public record ValidationResult(bool IsValid, IReadOnlyList<ValidationIssue> Issues);

// Source-generated JSON context — required for Native AOT Lambda functions;
// shared by all consumers to ensure consistent serialization behaviour.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RawLayout))]
[JsonSerializable(typeof(CompiledLayout))]
[JsonSerializable(typeof(PreviewLayout))]
[JsonSerializable(typeof(ValidationResult))]
[JsonSerializable(typeof(IReadOnlyList<RawLayout>))]
[JsonSerializable(typeof(IReadOnlyList<CompiledLayout>))]
public partial class LayoutContractsJsonContext : JsonSerializerContext { }
```

### Interfaces

```csharp
// RawLayoutService — CRUD for the editor; also implements IRawLayoutStatusWriter (below).
// Editor consumers depend on IRawLayoutRepository only.
// LayoutProcessingService depends on IRawLayoutStatusWriter only.
// RawLayoutService implements both; neither consumer gets more surface than it needs.
interface IRawLayoutRepository
{
    Task<RawLayout?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<RawLayout>> ListByUserAsync(string userId, CancellationToken ct);
    Task<RawLayout> SaveAsync(RawLayout layout, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

// Narrow write-back interface for LayoutProcessingService to record compilation results
// on a raw layout without requiring full CRUD access to RawLayoutService.
interface IRawLayoutStatusWriter
{
    Task UpdateValidationResultAsync(Guid rawLayoutId, ValidationResult result, CancellationToken ct);
}

// RawLayoutService → LayoutProcessingService — SQS-backed; enqueues a message on layout save
interface ILayoutProcessingTrigger
{
    Task TriggerAsync(Guid rawLayoutId, CancellationToken ct);
}

// LayoutProcessingService injects IRawLayoutRepository (shared with the editor) to fetch
// the raw layout by ID after dequeuing an SQS message. It also injects IRawLayoutStatusWriter
// (separate narrow interface) to write the ValidationResult back on completion.

// LayoutCompilerService — stateless; called by LayoutProcessingService (CompileAsync)
// and by RawLayoutService (CompilePreviewAsync) for the live preview endpoint.
// CompilePreviewAsync takes only the elements — no stored RawLayout record needed.
interface ILayoutCompilerClient
{
    Task<CompiledLayout> CompileAsync(RawLayout raw, CancellationToken ct);
    Task<PreviewLayout> CompilePreviewAsync(IReadOnlyList<RawLayoutElementDto> elements, CancellationToken ct);
}

// LayoutValidationService — stateless; called by LayoutProcessingService
interface ILayoutValidationClient
{
    Task<ValidationResult> ValidateAsync(CompiledLayout compiled, CancellationToken ct);
}

// CompiledLayoutService — storage and retrieval
// SetActiveAsync sets IsActive = true on the specified layout and clears it on all
// other layouts for the same user.
interface ICompiledLayoutRepository
{
    Task<CompiledLayout?> GetActiveForUserAsync(string userId, CancellationToken ct);
    Task<IReadOnlyList<CompiledLayout>> ListByUserAsync(string userId, CancellationToken ct);
    Task<CompiledLayout?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<CompiledLayout> SaveAsync(CompiledLayout layout, CancellationToken ct);
    Task SetActiveAsync(Guid id, string userId, CancellationToken ct);
}

// NotificationService — called by RawLayoutService on save, and by LayoutProcessingService on publish
// SSE event types:
//   layout-saved → editor subscribes; used to detect concurrent saves on the same layout
//   layout-ready → client subscribes; triggers download of the new compiled layout
// Future: layout-error (compilation failed) can be added if polling for validation results proves insufficient
interface INotificationPublisher
{
    Task PublishLayoutSavedAsync(string userId, Guid rawLayoutId, CancellationToken ct);
    Task PublishLayoutReadyAsync(string userId, Guid compiledLayoutId, CancellationToken ct);
}
```

### REST API surface

**Endpoints consumed by the client application:**

```
GET  /layouts/compiled             → list compiled layouts for the authenticated user
GET  /layouts/compiled/active      → active compiled layout; 404 if none exists yet
GET  /layouts/compiled/{id}        → specific compiled layout by ID
PUT  /layouts/compiled/{id}/active → set a compiled layout as active
GET  /notifications/layouts/stream → SSE stream; emits layout-saved and layout-ready events
```

**Endpoints consumed by the editor application:**

```
GET    /layouts/raw               → list raw layouts for the authenticated user
GET    /layouts/raw/{id}          → fetch a specific raw layout by ID
POST   /layouts/raw               → create a new raw layout (triggers compilation)
PUT    /layouts/raw/{id}          → update a raw layout (triggers recompilation)
DELETE /layouts/raw/{id}          → delete a raw layout
POST   /layouts/raw/preview       → compile a live preview from unsaved elements (no storage);
                                    request body: IReadOnlyList<RawLayoutElementDto>;
                                    returns PreviewLayout
```

**Internal endpoints (not exposed via API Gateway):**

```
POST /compile                     → LayoutCompilerService: compile a raw layout to CompiledLayout
POST /compile/preview             → LayoutCompilerService: compile elements to PreviewLayout
POST /validate                    → LayoutValidationService: validate a compiled layout
```

### Data flow: publish a layout (administrator saves)

1. Editor `POST`s or `PUT`s a raw layout → `RawLayoutService` stores it in DynamoDB
2. `RawLayoutService` calls `INotificationPublisher.PublishLayoutSavedAsync(userId, rawLayoutId)`
   → `NotificationService` pushes `layout-saved` SSE event to any connected editor for that user
   (concurrent-edit awareness — the saving editor and any others watching the same layout are notified)
3. `RawLayoutService` calls `ILayoutProcessingTrigger.TriggerAsync(rawLayoutId)` → SQS message enqueued;
   returns `201 Created` to the editor
4. `LayoutProcessingService` dequeues the SQS message, fetches the raw layout from `RawLayoutService`
5. Calls `ILayoutCompilerClient.CompileAsync(raw)` → `LayoutCompilerService` returns compiled layout
6. Calls `ILayoutValidationClient.ValidateAsync(compiled)` → `LayoutValidationService` returns `ValidationResult`
7. If valid: stores compiled layout via `CompiledLayoutService`; if invalid: the failure is
   recorded on the `RawLayout` record (`ValidationResult`) so the editor can display it on next fetch
8. If valid: calls `INotificationPublisher.PublishLayoutReadyAsync(userId, compiledId)`
   → `NotificationService` pushes `layout-ready` SSE event to any connected client for that user

### Data flow: client startup

1. Client authenticates with Cognito using Client Credentials flow; receives JWT
2. Client `GET /layouts/compiled/active` → receives active compiled layout and caches it
   locally; if `404`, client falls back to a bundled default layout until one is published
3. Client opens SSE connection: `GET /notifications/layouts/stream`
4. On SSE `layout-ready` event: client re-fetches `GET /layouts/compiled/active`, applies
   when user is idle

### Local development

All services run locally via `docker-compose`. A **LocalStack** container provides local emulation of DynamoDB, SQS, and Lambda. A
**dedicated Cognito dev user pool** handles JWT issuance and validation — real Cognito is
used rather than a local stub to ensure auth behavior matches production exactly. AWS
credentials are required for local development (for both LocalStack and Cognito). All internal
service-to-service URLs are resolved via Docker DNS. The client application is configured
to point to the local backend via `appsettings.Development.json`.

## Related Epics

The following epics will each receive their own spec before implementation begins.

| Epic | Scope |
|------|-------|
| **[ADR-161](https://jodasoft.atlassian.net/browse/ADR-161)** (this) | Backend services: storage, compilation, validation, processing, notifications |
| **[ADR-162](https://jodasoft.atlassian.net/browse/ADR-162)** | Client-side layout consumption: download, cache, apply, auto-update |
| **[ADR-163](https://jodasoft.atlassian.net/browse/ADR-163)** | Blazor WebAssembly editor: text editor + live preview |
| **[ADR-164](https://jodasoft.atlassian.net/browse/ADR-164)** | AWS CI/CD deployment pipeline: containerized deployment to AWS |
| **[ADR-165](https://jodasoft.atlassian.net/browse/ADR-165)** | Stress testing and availability: bot accounts, load scenarios, availability metrics |

## Open Questions

- [x] ~~Which external IdP will be used in production?~~ **Resolved:** AWS Cognito.
  Authorization Code flow for editor users; Client Credentials flow for the client
  application and bot accounts. Dev environment uses a dedicated Cognito dev user pool.
- [x] ~~Should layout compilation be synchronous or asynchronous?~~ **Resolved:** Async, by
  virtue of using SQS. `RawLayoutService` returns `201 Created` immediately; compilation
  result is delivered via SSE (`layout-ready` or `layout-error`).
- [ ] What validation rules does `LayoutValidationService` enforce? The structure is defined
  by `RawLayoutElementDto` (nested hierarchy of `RawCommandDefinitionDto` and
  `RawLayoutGroupDefinitionDto`; grid position and per-element CSS overrides included).
  The specific constraints (e.g. valid grid ranges, required fields, CSS syntax) must be
  defined before `LayoutValidationService` can be implemented. This is a dependency on the
  editor epic.
- [x] ~~Can a user have multiple named layouts?~~ **Resolved:** Multiple layouts per user
  are supported from the start; one is designated active. `CompiledLayout` carries `UserId`
  and `IsActive`. A dedicated endpoint sets the active layout. Client-side support for
  switching layouts (e.g. by input source) is deferred to a future client epic.

## Related Docs

- [`src/_doc_Projects.md`](_doc_Projects.md)
- [`src/AdaptiveRemote.App/Services/_doc_Services.md`](AdaptiveRemote.App/Services/_doc_Services.md)
- [`src/AdaptiveRemote.App/Services/Commands/_doc_Commands.md`](AdaptiveRemote.App/Services/Commands/_doc_Commands.md)
- [`src/AdaptiveRemote.App/Services/Lifecycle/_doc_Lifecycle.md`](AdaptiveRemote.App/Services/Lifecycle/_doc_Lifecycle.md)
- [`src/AdaptiveRemote.App/Services/ProgrammaticSettings/_doc_ProgrammaticSettings.md`](AdaptiveRemote.App/Services/ProgrammaticSettings/_doc_ProgrammaticSettings.md)

## Tasks

### Task 1 — Repo reorganization and shared contracts ([ADR-166](https://jodasoft.atlassian.net/browse/ADR-166))

Add solution filters and the `AdaptiveRemote.Contracts` shared library.

- [ ] `client.slnf` and `backend.slnf` solution filters created; both build cleanly with `dotnet build /warnaserror`
- [ ] `AdaptiveRemote.Contracts` project created; targets `net10.0` (no `-windows`); no platform-specific dependencies
- [ ] All DTOs, enums, interfaces, and `LayoutContractsJsonContext` from the spec's Shared Contracts section are implemented
- [ ] `AdaptiveRemote.Contracts` is referenced by `AdaptiveRemote.App` and builds without warnings
- [ ] All existing client unit tests and headless E2E tests pass

### Task 2 — Static layout MVP ([ADR-167](https://jodasoft.atlassian.net/browse/ADR-167))

Create `AdaptiveRemote.Backend.CompiledLayoutService` returning the current hardcoded layout.
Establish the backend API integration test infrastructure, the observability pattern (health
endpoints, structured logging, metrics), and the log validation pattern for API tests. All
subsequent backend services follow these patterns from the start.

- [ ] `AdaptiveRemote.Backend.CompiledLayoutService` project created under `src/`; included in `backend.slnf`
- [ ] `GET /layouts/compiled/active` returns the current hardcoded layout serialized as `CompiledLayout` using `LayoutContractsJsonContext`
- [ ] No auth required for this task; endpoint is unauthenticated
- [ ] `GET /health` implemented; returns `200 OK` with service name and version; **this pattern is required for all subsequent backend services**
- [ ] Structured logging pattern established: log messages defined as `[LoggerMessage]` source-generated methods (same discipline as `MessageLogger.cs` in the client app); request/response logging middleware applied; **this pattern is required for all subsequent backend services**
- [ ] Metrics pattern established: key operations emit structured log events that serve as the local-dev metrics signal (e.g. request count, status code); CloudWatch as the production sink is deferred to the CI/CD deployment epic; **this pattern is required for all subsequent backend services**
- [ ] `docker-compose` configured so structured log output is visible for all running services in local dev
- [ ] Service runs in `docker-compose` and is reachable from the client app via `appsettings.Development.json`
- [ ] Backend API integration test project created (e.g. `AdaptiveRemote.Backend.ApiTests`);
  includes an `HttpClient` fixture that spins up services via `docker-compose` and is
  runnable against local dev, CI, and deployed environments; captures structured log output
  from each service so Gherkin scenarios can assert on expected log events and the absence
  of warnings or errors; pattern documented for reuse in subsequent tasks
- [ ] API integration tests cover `GET /layouts/compiled/active` and `GET /health`:

  ```gherkin
  Given CompiledLayoutService is running
  When a test client calls GET /layouts/compiled/active
  Then the response is 200 OK
  And the body deserializes to a valid CompiledLayout using LayoutContractsJsonContext
  And the CompiledLayout contains the expected hardcoded commands
  And the service logs contain a request log entry for GET /layouts/compiled/active
  And the service logs contain no warnings or errors

  Given CompiledLayoutService is running
  When a test client calls GET /health
  Then the response is 200 OK
  And the body contains the service name and version
  ```

- [ ] All existing headless E2E tests pass with the client reading from the service

### Task 3 — Auth integration (Cognito) ([ADR-168](https://jodasoft.atlassian.net/browse/ADR-168))

Wire up JWT validation via AWS Cognito and API Gateway before any user-specific storage is built. Establishing auth at this stage surfaces Cognito unknowns (dev user pool setup, JWT issuance, JWKS validation) while the service count is still low, and ensures every subsequent task builds on a working auth layer from the start rather than retrofitting it across multiple services at once.

- [ ] Cognito dev user pool created; JWKS endpoint configured in API Gateway
- [ ] API Gateway validates JWT bearer tokens on all external endpoints; unauthenticated requests return `401`
- [ ] `CompiledLayoutService` extracts the `sub` claim as `userId`; Task 2 API integration tests updated to include valid JWT headers
- [ ] Client app configured with `client_id` / `client_secret` via `appsettings.Development.json`; acquires and refreshes tokens automatically in the background
- [ ] Editor app auth flow (Authorization Code) documented with setup instructions for local dev
- [ ] Internal endpoints (Lambda Function URLs) are network-isolated and not exposed via API Gateway
- [ ] `GET /health` added to `CompiledLayoutService`; logging and metrics pattern from Task 2 verified under authenticated requests; API integration tests updated to assert no warnings or errors in service logs
- [ ] API integration tests cover authentication enforcement:

  ```gherkin
  Given a request with no Authorization header
  When a test client calls GET /layouts/compiled/active
  Then the response is 401 Unauthorized

  Given a request with a valid Cognito JWT
  When a test client calls GET /layouts/compiled/active
  Then the response is 200 OK

  Given a request with an expired Cognito JWT
  When a test client calls GET /layouts/compiled/active
  Then the response is 401 Unauthorized
  ```

### Task 4 — RawLayoutService + DynamoDB ([ADR-169](https://jodasoft.atlassian.net/browse/ADR-169))

Implement `AdaptiveRemote.Backend.RawLayoutService` with full CRUD backed by DynamoDB.

- [ ] `AdaptiveRemote.Backend.RawLayoutService` project created; included in `backend.slnf`
- [ ] `IRawLayoutRepository` and `IRawLayoutStatusWriter` implemented against DynamoDB (LocalStack in dev)
- [ ] DynamoDB table created with partition key `UserId`, sort key `Id` (KSUID)
- [ ] All CRUD endpoints (`GET /layouts/raw`, `GET /layouts/raw/{id}`, `POST /layouts/raw`, `PUT /layouts/raw/{id}`, `DELETE /layouts/raw/{id}`) implemented and unit tested
- [ ] `docker-compose.yml` updated with LocalStack container; DynamoDB table provisioned on startup
- [ ] `ILayoutProcessingTrigger` stub (no-op) injected so save/update endpoints compile; SQS wiring deferred to Task 5
- [ ] `INotificationPublisher` stub (no-op) injected; notification wiring deferred to Task 9
- [ ] Follows the logging, metrics, and health endpoint pattern established in Task 2; API integration tests assert no warnings or errors in service logs during normal CRUD operations
- [ ] Unit tests cover repository logic against LocalStack or mocked DynamoDB client
- [ ] API integration tests cover all CRUD endpoints:

  ```gherkin
  Given an authenticated user has no raw layouts
  When a test client calls GET /layouts/raw
  Then the response is 200 OK
  And the body is an empty array

  Given an authenticated user
  When a test client calls POST /layouts/raw with a valid RawLayout body
  Then the response is 201 Created
  And the body contains the created RawLayout with a generated Id
  And GET /layouts/raw/{id} returns the same layout

  Given a raw layout exists with id {id}
  When a test client calls PUT /layouts/raw/{id} with updated elements
  Then the response is 200 OK
  And GET /layouts/raw/{id} returns the updated elements

  Given a raw layout exists with id {id}
  When a test client calls DELETE /layouts/raw/{id}
  Then the response is 204 No Content
  And GET /layouts/raw/{id} returns 404 Not Found
  ```

### Task 5 — Development environment support ([ADR-187](https://jodasoft.atlassian.net/browse/ADR-187))

Establish a consistent developer experience across all backend services: local launch with a
separate console window, an interactive API browser (Scalar), startup dependency health checks
with actionable error messages, and a debuggable local invocation story for Lambda functions.
Applied retroactively to all services built in Tasks 1–4; required for all subsequent services.

**ECS Fargate services** (CompiledLayoutService, RawLayoutService; pattern required for all
future Fargate services):

- [ ] `Microsoft.AspNetCore.OpenApi` and `Scalar.AspNetCore` packages added; Scalar UI
  registered via `app.MapScalarApiReference()` guarded by `app.Environment.IsDevelopment()`;
  accessible at `/scalar` when running locally; **not** reachable in staging or production
- [ ] `launchSettings.json` includes a `Development` launch profile with
  `"outputCapture": "None"` so F5 in VS opens a separate console window (not the VS Output
  pane); `dotnet run` already outputs to the console natively — no extra config needed
- [ ] On startup, each service pings `/_localstack/health` on the configured LocalStack base
  URL; if the request fails or returns a non-`running` status, a `[LoggerMessage]`-defined
  `Error`-level message is emitted that names LocalStack as the missing dependency and
  includes `"See docs/local-dev.md for setup instructions"`; `Environment.Exit(1)` is called
  immediately after
- [ ] `docs/local-dev.md` created at the repo root; covers: Docker and Docker Compose
  installation and the `docker-compose up -d` start command, confirming LocalStack is healthy
  at `/_localstack/health`, and Cognito dev user pool credential setup; referenced from the
  startup error message above

**Lambda functions** (LayoutCompilerService, LayoutValidationService; pattern required for all
future Lambda services):

- [ ] `amazon-lambda-testtool` (latest version supporting .NET 10) installed globally;
  `launchSettings.json` includes a profile that launches the test tool so F5 in VS opens
  the Lambda Test Tool UI for interactive invocation and debugging
- [ ] LocalStack Lambda emulation verified: function is deployed to LocalStack via
  `docker-compose` on `up`, and invokable with
  `aws lambda invoke --endpoint-url http://localhost:4566 --function-name <name> --payload '<json>' response.json`
- [ ] Sample invocation commands for each Lambda function (with minimal valid payloads)
  documented in `docs/local-dev.md`

**Shared — standing pattern for all future tasks:**

- [ ] `src/_doc_BackendDevelopment.md` created; documents the agent verification step:
  after every change to a backend service, run the service with LocalStack running (confirm
  clean start) and with LocalStack stopped (confirm the startup error message and non-zero
  exit); this doc is added to the CLAUDE.md "Read Before Making Changes" table under
  "Backend services"
- [ ] All existing backend services (Tasks 1–4 outputs) retrofitted to meet the above
  checklist; `dotnet build /warnaserror` passes; existing API integration tests pass

```gherkin
Given LocalStack is not running
When a developer runs dotnet run for any ECS Fargate backend service
Then the process exits with a non-zero exit code
And the console output names LocalStack as the missing dependency
And the console output includes "See docs/local-dev.md for setup instructions"

Given LocalStack is running
When a developer runs dotnet run for any ECS Fargate backend service
Then the service starts successfully
And navigating to /scalar in a browser shows the Scalar API UI
And log output is visible in a separate console window

Given LocalStack is running with the Lambda function deployed
When a developer invokes the Lambda via aws cli with --endpoint-url http://localhost:4566
Then the Lambda returns a valid response without error

Given the Lambda Test Tool is installed
When a developer launches the Lambda project with F5 in Visual Studio
Then the Lambda Test Tool UI opens in a browser for interactive invocation
```

### Task 6 — LayoutProcessingService (with stubs) ([ADR-170](https://jodasoft.atlassian.net/browse/ADR-170))

Implement `AdaptiveRemote.Backend.LayoutProcessingService` with SQS polling and the full
orchestration pipeline. `ILayoutCompilerClient` and `ILayoutValidationClient` are backed by
stub implementations that return hardcoded valid results, keeping the pipeline testable
end-to-end before the real Lambda functions are built in Tasks 6 and 7.

- [ ] `AdaptiveRemote.Backend.LayoutProcessingService` project created; included in `backend.slnf`
- [ ] SQS queue and DLQ provisioned in `docker-compose` via LocalStack; max receive count = 3; DLQ retention = 14 days
- [ ] `ILayoutCompilerClient` stub returns a hardcoded `CompiledLayout` derived from the input `RawLayout` elements (names and labels passed through; no real CSS generation)
- [ ] `ILayoutValidationClient` stub returns `ValidationResult { IsValid = true, Issues = [] }`
- [ ] Service polls SQS queue and processes messages: fetch raw layout → compile → validate → store compiled → notify
- [ ] On validation failure: calls `IRawLayoutStatusWriter.UpdateValidationResultAsync`; does not store a compiled layout; does not notify client
- [ ] On success: calls `ICompiledLayoutRepository.SaveAsync` then `INotificationPublisher.PublishLayoutReadyAsync`
- [ ] Failed processing attempts are logged as errors; DLQ arrival is logged as an error
- [ ] `RawLayoutService` SQS trigger wired up (replaces no-op stub from Task 4)
- [ ] `INotificationPublisher` stub (no-op) injected; notification wiring deferred to Task 9
- [ ] Follows the logging, metrics, and health endpoint pattern established in Task 2; structured log events emitted on each SQS message processed (success and failure); API integration tests assert expected log events and no unexpected warnings or errors
- [ ] Unit tests cover success path, validation failure path, and SQS message retry behaviour
- [ ] API integration tests cover the end-to-end processing pipeline (stub compiler and validator in use):

  ```gherkin
  Given a raw layout with valid elements has been saved via POST /layouts/raw
  When LayoutProcessingService dequeues and processes the SQS message
  Then GET /layouts/compiled/active returns a CompiledLayout for the user
  And the CompiledLayout.Elements match the commands from the raw layout

  Given a raw layout with a command missing a Label has been saved via POST /layouts/raw
  When LayoutProcessingService dequeues and processes the SQS message
  Then no compiled layout is stored for the user
  And GET /layouts/raw/{id} returns a RawLayout with a non-null ValidationResult
  And ValidationResult.IsValid is false
  ```
- [ ] Follows the dev environment pattern from Task 5: Scalar UI configured and guarded by
  `IsDevelopment()`, console window launch profile present, LocalStack startup health check
  implemented, and agent verification step completed (start with LocalStack running; start
  with LocalStack stopped and confirm startup error)

### Task 7 — LayoutCompilerService (Lambda) ([ADR-171](https://jodasoft.atlassian.net/browse/ADR-171))

Implement `AdaptiveRemote.Backend.LayoutCompilerService` as a Native AOT Lambda, replacing
the stub injected in Task 5.

- [ ] `AdaptiveRemote.Backend.LayoutCompilerService` project created as a .NET 10 Lambda function with Native AOT; included in `backend.slnf`
- [ ] `POST /compile` accepts `RawLayout`, returns `CompiledLayout`; grid positions and CSS overrides resolved into `CssDefinitions`; layout elements stripped of authoring properties
- [ ] `POST /compile/preview` accepts `IReadOnlyList<RawLayoutElementDto>`, returns `PreviewLayout` with rendered HTML and CSS
- [ ] All serialization uses `LayoutContractsJsonContext`; no reflection-based JSON
- [ ] Lambda runs locally via LocalStack; `LayoutProcessingService` `ILayoutCompilerClient` stub replaced with real Lambda-backed implementation
- [ ] Follows the logging, metrics, and health endpoint pattern established in Task 2; Lambda invocation events logged; API integration tests assert no warnings or errors during successful compilation
- [ ] Unit tests cover compilation logic for representative layout inputs
- [ ] API integration tests cover both endpoints (called directly via Lambda Function URL):

  ```gherkin
  Given a valid RawLayout with one command element at grid position (1, 1)
  When a test client calls POST /compile with the RawLayout
  Then the response is 200 OK
  And the body deserializes to a CompiledLayout
  And CompiledLayout.Elements contains a CommandDefinitionDto matching the input command
  And CompiledLayout.CssDefinitions contains a CSS rule for the element's grid position
  And the CommandDefinitionDto does not contain grid or CSS authoring properties

  Given a valid list of RawLayoutElementDto
  When a test client calls POST /compile/preview with the elements
  Then the response is 200 OK
  And the body deserializes to a PreviewLayout
  And PreviewLayout.RenderedHtml is non-empty
  And PreviewLayout.RenderedCss is non-empty
  ```
- [ ] Follows the Lambda dev environment pattern from Task 5: Lambda Test Tool launch profile
  present, LocalStack deployment verified via `aws lambda invoke`, and agent verification step
  completed

### Task 8 — LayoutValidationService (Lambda) ([ADR-172](https://jodasoft.atlassian.net/browse/ADR-172))

Implement `AdaptiveRemote.Backend.LayoutValidationService` as a Native AOT Lambda, replacing
the stub injected in Task 5.

- [ ] `AdaptiveRemote.Backend.LayoutValidationService` project created as a .NET 10 Lambda function with Native AOT; included in `backend.slnf`
- [ ] `POST /validate` accepts `CompiledLayout`, returns `ValidationResult`
- [ ] Validates that all `CommandDefinitionDto` entries have non-empty `Name`, `Label`, and `SpeakPhrase`; duplicate `CssId` values within a layout are flagged
- [ ] Additional validation rules deferred pending editor epic (see Open Questions)
- [ ] All serialization uses `LayoutContractsJsonContext`; no reflection-based JSON
- [ ] `LayoutProcessingService` `ILayoutValidationClient` stub replaced with real Lambda-backed implementation
- [ ] Follows the logging, metrics, and health endpoint pattern established in Task 2; validation outcome (pass/fail, issue count) emitted as a structured log event; API integration tests assert no unexpected warnings or errors
- [ ] Unit tests cover valid layout, missing required fields, and duplicate CSS IDs
- [ ] API integration tests cover both valid and invalid cases (called directly via Lambda Function URL):

  ```gherkin
  Given a CompiledLayout where all commands have non-empty Name, Label, and SpeakPhrase
  And all CssId values are unique
  When a test client calls POST /validate with the CompiledLayout
  Then the response is 200 OK
  And ValidationResult.IsValid is true
  And ValidationResult.Issues is empty

  Given a CompiledLayout where one command has an empty Label
  When a test client calls POST /validate with the CompiledLayout
  Then the response is 200 OK
  And ValidationResult.IsValid is false
  And ValidationResult.Issues contains one issue referencing the empty Label

  Given a CompiledLayout where two elements share the same CssId
  When a test client calls POST /validate with the CompiledLayout
  Then the response is 200 OK
  And ValidationResult.IsValid is false
  And ValidationResult.Issues contains one issue referencing the duplicate CssId
  ```
- [ ] Follows the Lambda dev environment pattern from Task 5: Lambda Test Tool launch profile
  present, LocalStack deployment verified via `aws lambda invoke`, and agent verification step
  completed

### Task 9 — CompiledLayoutService with DynamoDB ([ADR-173](https://jodasoft.atlassian.net/browse/ADR-173))

Replace the static hardcoded response in `CompiledLayoutService` with real DynamoDB storage and active layout management.

- [ ] `ICompiledLayoutRepository` implemented against DynamoDB
- [ ] `GetActiveForUserAsync`, `ListByUserAsync`, `GetByIdAsync`, `SaveAsync`, and `SetActiveAsync` all implemented and unit tested
- [ ] `SetActiveAsync` sets `IsActive = true` on the specified layout and clears it on all other layouts for the same user (via DynamoDB transaction or conditional writes)
- [ ] Follows the logging, metrics, and health endpoint pattern established in Task 2; API integration tests assert no warnings or errors during normal storage operations
- [ ] All compiled layout endpoints functional end-to-end with DynamoDB
- [ ] `PUT /layouts/compiled/{id}/active` endpoint implemented
- [ ] Previously hardcoded layout seeded into DynamoDB on first run so the client continues to work
- [ ] API integration tests cover the 404 case and active layout switching:

  ```gherkin
  Given no compiled layout exists for the user
  When a test client calls GET /layouts/compiled/active
  Then the response is 404 Not Found

  Given a user has two compiled layouts and layout B is active
  When a test client calls PUT /layouts/compiled/{A}/active
  Then the response is 200 OK
  And GET /layouts/compiled/active returns layout A
  And layout B is no longer active
  ```
- [ ] Follows the dev environment pattern from Task 5: Scalar UI configured and guarded by
  `IsDevelopment()`, console window launch profile present, LocalStack startup health check
  implemented, and agent verification step completed (start with LocalStack running; start
  with LocalStack stopped and confirm startup error)

### Task 10 — NotificationService (SSE) ([ADR-174](https://jodasoft.atlassian.net/browse/ADR-174))

Implement `AdaptiveRemote.Backend.NotificationService` with SSE push for `layout-saved` and `layout-ready` events.

- [ ] `AdaptiveRemote.Backend.NotificationService` project created; included in `backend.slnf`
- [ ] `GET /notifications/layouts/stream` SSE endpoint implemented; connection is keyed to the authenticated user
- [ ] `INotificationPublisher` implementation sends `layout-saved` events to connected editors and `layout-ready` events to connected clients for the relevant user
- [ ] Standard SSE retry mechanism honoured; disconnected clients reconnect automatically
- [ ] `RawLayoutService` and `LayoutProcessingService` notification stubs replaced with real `INotificationPublisher` implementation
- [ ] Follows the logging, metrics, and health endpoint pattern established in Task 2; SSE connection lifecycle events (connect, disconnect, reconnect) emitted as structured log events
- [ ] Unit tests cover event publishing and per-user fan-out

  ```gherkin
  Given a client is connected to the SSE stream
  And the administrator publishes a new compiled layout
  When LayoutProcessingService completes successfully
  Then the client receives a layout-ready SSE event
  And fetching GET /layouts/compiled/active returns the new layout

  Given two editor sessions are open for the same layout
  When one editor saves the layout
  Then both editors receive a layout-saved SSE event
  ```
- [ ] Follows the dev environment pattern from Task 5: Scalar UI configured and guarded by
  `IsDevelopment()`, console window launch profile present, LocalStack startup health check
  implemented, and agent verification step completed (start with LocalStack running; start
  with LocalStack stopped and confirm startup error)

---

### [ADR-162](https://jodasoft.atlassian.net/browse/ADR-162): Client-side layout consumption

Implement layout download, local caching, compiled layout application, and auto-update on `layout-ready` SSE event in the client app. Includes the mapping from `CommandDefinitionDto` → runtime `Command` types and the idle-detection policy for deferred layout application.

### [ADR-163](https://jodasoft.atlassian.net/browse/ADR-163): Blazor WebAssembly editor

Implement the administrator-facing editor application: text editor for raw layout JSON, live preview via `POST /layouts/raw/preview`, and layout management (create, update, delete, set active).

### [ADR-164](https://jodasoft.atlassian.net/browse/ADR-164): AWS CI/CD deployment pipeline

Containerize all ECS Fargate services; package Lambda functions; define infrastructure as code (ECS task definitions, API Gateway configuration, DynamoDB tables, SQS queues); automate deployment to AWS on merge to main. Includes wiring the CloudWatch metrics sink (replacing the local structured-log-based signal established in Task 2), CloudWatch alarms (DLQ depth > 0, error rate thresholds), and ECS health check integration.

### [ADR-165](https://jodasoft.atlassian.net/browse/ADR-165): Stress testing and availability

Define bot account provisioning via Cognito API; implement load generation scenarios; instrument availability and latency metrics; establish baseline SLOs.
