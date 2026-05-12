# Backend Development Guide

This document defines the standing development pattern for backend services introduced by
Task 5 ([ADR-187](https://jodasoft.atlassian.net/browse/ADR-187)).

## Services

| Service | Port (dev) | Notes |
|---------|------------|-------|
| `AdaptiveRemote.Backend.CompiledLayoutService` | 54433 (HTTPS) / 54434 (HTTP) | Compiled layout storage and retrieval |
| `AdaptiveRemote.Backend.RawLayoutService` | 54435 (HTTPS) / 54436 (HTTP) | Raw layout CRUD; enqueues SQS trigger on save |
| `AdaptiveRemote.Backend.LayoutProcessingService` | 54437 (HTTPS) / 54438 (HTTP) | SQS polling; orchestrates compile → validate → store → notify pipeline |
| `AdaptiveRemote.Backend.LayoutCompilerService` | 5180 (HTTP) | Compiles raw layouts to CSS+element DTOs; runs as a plain HTTP container in Docker Compose (Lambda deployment is aspirational) |

### LayoutProcessingService

`AdaptiveRemote.Backend.LayoutProcessingService` is the orchestration service for the layout
compilation pipeline. It polls an SQS queue (`LayoutProcessingQueue`) for raw layout IDs,
then drives: fetch raw layout → compile → validate → store compiled layout → publish notification.

**Pipeline steps:**

1. Dequeue SQS message containing `rawLayoutId`
2. Fetch `RawLayout` from `RawLayoutService` via `IRawLayoutRepository`
3. Compile via `HttpLayoutCompilerClient` → `LayoutCompilerService` (POST /compile)
4. Validate via `ILayoutValidationClient` (stub: `StubLayoutValidationClient`)
5a. On validation failure: write result back via `IRawLayoutStatusWriter`; delete message
5b. On success: store compiled layout via `ICompiledLayoutRepository`; publish notification via `INotificationPublisher`; delete message
5c. On error: do NOT delete message; SQS retry → DLQ (max receive count = 3; DLQ retention = 14 days)

**Compiler client:**

`ILayoutCompilerClient` is implemented by `HttpLayoutCompilerClient`, which calls `LayoutCompilerService` (POST /compile). The previous `StubLayoutCompilerClient` has been removed.

**Stub implementations (remaining):**

- `StubLayoutValidationClient` — always returns `IsValid=true`; set `Validation:ForceInvalid=true` to exercise the failure path
- `StubNotificationPublisher` — no-op

**Service-to-service auth:** When calling `RawLayoutService`, the HTTP clients attach a bearer
token if `RawLayoutService:ServiceAccountToken` is configured. In production this will be
replaced by Cognito M2M or IAM-signed requests.

**SQS queue config (LocalStack):** provisioned by `docker-compose`; max receive count = 3;
DLQ retention = 14 days; DLQ name = `LayoutProcessingQueue-dlq`.

## ECS/Fargate-style API services

All backend API services must follow this local development pattern:

1. Register OpenAPI and map Scalar UI only in development (`/scalar`).
2. Include a `Development` launch profile with `"outputCapture": "None"` so F5 opens a
   separate console window in Visual Studio.
3. On startup (development), check `/_localstack/health` on the configured LocalStack base URL.
   If unavailable or not `running`, log an error that names LocalStack and references
   `docs/local-dev.md`, then exit non-zero immediately.

## Lambda services

All backend Lambda projects must include:

1. A launch profile that starts the Lambda Test Tool for interactive local debugging.
2. LocalStack deployment support through `docker-compose`.
3. Documented `aws lambda invoke --endpoint-url http://localhost:4566` sample commands.

## Agent Verification Step (required after backend changes)

After every backend service change:

1. **With LocalStack running:** run the service and confirm clean startup plus `/scalar` availability.
2. **With LocalStack stopped:** run the service and confirm non-zero exit with the LocalStack
   dependency error message that includes `docs/local-dev.md`.

For Lambda services:

1. Confirm the Lambda Test Tool profile launches successfully.
2. Confirm `aws lambda invoke --endpoint-url http://localhost:4566` returns a valid response.

See `docs/local-dev.md` for setup and invocation details.
