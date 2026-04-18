# Backend Development Guide

> **Status:** Stub — to be populated during Task 5 ([ADR-187](https://jodasoft.atlassian.net/browse/ADR-187))
>
> See `src/_spec_LayoutCustomizationService.md` Task 5 for the full exit criteria.

## Agent Verification Step

After every change to a backend service, verify the development environment still works:

1. **With LocalStack running:** `dotnet run` (or F5 in VS) → confirm the service starts cleanly, log output appears in a console window, and `/scalar` is reachable in a browser
2. **With LocalStack stopped:** `dotnet run` → confirm the process exits with a non-zero code and the console names LocalStack as the missing dependency with a reference to `docs/local-dev.md`

For Lambda functions:
1. Confirm F5 in VS opens the Lambda Test Tool UI
2. Confirm `aws lambda invoke --endpoint-url http://localhost:4566` returns a valid response

> This section will be expanded with full setup details and patterns once Task 5 is implemented.
