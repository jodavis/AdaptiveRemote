# Backend Development Guide

This document defines the standing development pattern for backend services introduced by
Task 5 ([ADR-187](https://jodasoft.atlassian.net/browse/ADR-187)).

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
