# Local Backend Development

This guide covers local backend dependencies for AdaptiveRemote backend services.

> Current repository state: `AdaptiveRemote.Backend.CompiledLayoutService` is the only
> backend API service currently implemented in `src/`. Apply the same startup and `/scalar`
> checks to additional backend services as they are added.

## Prerequisites

1. Install Docker Desktop (or Docker Engine + Docker Compose plugin).
2. Verify tools:
   - `docker --version`
   - `docker compose version`
3. From the repository root, start local dependencies:

   ```bash
   docker compose up -d
   ```

## Confirm LocalStack health

LocalStack must report `status: running`:

```bash
curl http://localhost:4566/_localstack/health
```

Expected response contains LocalStack health JSON with either:

```json
{ "status": "running" }
```

or service entries showing required services as available/running, for example:

```json
{
  "services": {
    "dynamodb": "available",
    "lambda": "available",
    "sqs": "available"
  }
}
```

## Cognito development credentials

Set Cognito values for backend services (for `docker-compose` these map to
`COGNITO_AUTHORITY` and `COGNITO_AUDIENCE`):

- `Cognito__Authority` / `COGNITO_AUTHORITY`
- `Cognito__Audience` / `COGNITO_AUDIENCE` (optional)

See `src/AdaptiveRemote.Backend.CompiledLayoutService/_doc_Auth.md`
for full Cognito dev user pool setup.

## Scalar API browser

When running backend API services in development, Scalar is available at:

- `http://localhost:<port>/scalar`

Scalar is development-only and is not mapped in non-development environments.

## Lambda local debugging

Install the Lambda test tool globally (latest .NET 10-compatible package):

```bash
dotnet tool install -g Amazon.Lambda.TestTool-10.0
```

Use a launch profile that starts the test tool for interactive invocation/debugging.

## LocalStack Lambda invocation samples

Use `--endpoint-url http://localhost:4566` for local invocation.

### LayoutCompilerService

```bash
aws lambda invoke \
  --endpoint-url http://localhost:4566 \
  --function-name adaptiveremote-layout-compiler-dev \
  --payload '{"id":"00000000-0000-0000-0000-000000000001","userId":"test-user","elements":[]}' \
  response-layout-compiler.json
```

### LayoutValidationService

```bash
aws lambda invoke \
  --endpoint-url http://localhost:4566 \
  --function-name adaptiveremote-layout-validation-dev \
  --payload '{"id":"00000000-0000-0000-0000-000000000001","userId":"test-user","elements":[],"cssDefinitions":[]}' \
  response-layout-validation.json
```
