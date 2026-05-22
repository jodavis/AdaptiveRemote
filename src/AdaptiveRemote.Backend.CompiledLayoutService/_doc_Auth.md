# Authentication — CompiledLayoutService (ADR-168)

Summary: Describes JWT Bearer authentication via AWS Cognito for external API endpoints in the CompiledLayoutService.

## Overview

External API endpoints are protected by JWT Bearer authentication backed by **AWS Cognito**.
The service validates bearer tokens on every protected request; `/health` is unauthenticated.

The `sub` claim from the validated JWT is used as the `userId` throughout the service.

## Authentication flows

| Client | Flow | Trigger |
|--------|------|---------|
| Client application (WPF) | OAuth2 **Client Credentials** | Unattended machine; acquires token at startup, refreshes automatically |
| Editor application (Blazor WASM) | OAuth2 **Authorization Code** | Browser-based interactive login via Cognito Hosted UI |

## Cognito dev user pool setup

1. Create a Cognito user pool in your AWS dev account.
2. Under **App clients**, create two app clients:
   - `adaptiveremote-client` — enable Client Credentials flow; note `client_id` and `client_secret`.
   - `adaptiveremote-editor` — enable Authorization Code flow; configure allowed callback URL.
3. Create a resource server (custom scope), e.g. `adaptiveremote/layouts.read`.
4. Note the user pool's **Issuer URL** (shown in the pool's details page):
   `https://cognito-idp.us-east-2.amazonaws.com/us-east-2_65NKvrlha`

## Configuring the backend service

Set these environment variables (or values in `appsettings.Development.json` — never commit secrets):

| Variable | Example |
|----------|---------|
| `Cognito__Authority` | `https://cognito-idp.us-east-2.amazonaws.com/us-east-2_65NKvrlha` |
| `Cognito__Audience` | `<app-client-id>` (optional; leave empty to skip audience validation) |

For local development via `docker-compose`, set `COGNITO_AUTHORITY` and `COGNITO_AUDIENCE` in a
`.env` file at the repository root (excluded from source control by `.gitignore`).

## Configuring the client application (Client Credentials)

Set in `appsettings.Development.json` (non-secret values) and user secrets (secrets):

```json
{
  "backend": {
    "baseUrl": "http://localhost:8080",
    "cognito": {
      "authority": "https://cognito-idp.us-east-2.amazonaws.com/us-east-2_65NKvrlha",
      "clientId": "5g6eqq1v1o7lju703enelssl89",
      "scope": "adaptiveremote/layouts.read"
    }
  }
}
```

Add `clientSecret` to user secrets only:
```bash
dotnet user-secrets set "backend:cognito:clientSecret" "YOUR_CLIENT_SECRET" \
  --project src/AdaptiveRemote/AdaptiveRemote.csproj
```

The `CognitoTokenService` in `src/AdaptiveRemote.App/Services/Backend/` discovers the token
endpoint from the OIDC configuration document and acquires/refreshes tokens automatically.

## Configuring the editor application (Authorization Code)

The editor application (Blazor WASM — separate epic) uses the Cognito Hosted UI for login.
The required configuration is:

1. Set the `adaptiveremote-editor` app client's callback URL to the editor's redirect URI.
2. Configure the editor app with `cognitoAuthorizeUrl`, `clientId`, and `redirectUri` (no
   client secret — public client, PKCE required).
3. On sign-in, the Cognito Hosted UI redirects back with an authorization code; the editor
   exchanges it for tokens using PKCE.

Full setup instructions will be added to the editor epic's documentation when implemented.

## Internal endpoints

`LayoutCompilerService` and `LayoutValidationService` are hosted as **AWS Lambda functions**
with **Lambda Function URLs**. These URLs are not exposed via API Gateway and are accessible
only from within the ECS cluster (network isolation via VPC/security groups). No bearer token
validation is required or expected on internal Lambda endpoints.

## Getting a test token (manual testing / Scalar)

To test protected endpoints manually (e.g. via the Scalar UI), you need a bearer token from
the `adaptiveremote-client` app client.

**Option 1 — curl**
```bash
curl -X POST https://us-east-265nkvrlha.auth.us-east-2.amazoncognito.com/oauth2/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=44qanfe7hvaeumffnt5hsk0ojr&client_secret=YOUR_CLIENT_SECRET&scope=adaptiveremote/layouts.read"
```

**Option 2 — browser console (no install required)**
```javascript
const resp = await fetch("https://us-east-265nkvrlha.auth.us-east-2.amazoncognito.com/oauth2/token", {
  method: "POST",
  headers: { "Content-Type": "application/x-www-form-urlencoded" },
  body: "grant_type=client_credentials&client_id=44qanfe7hvaeumffnt5hsk0ojr&client_secret=YOUR_CLIENT_SECRET&scope=adaptiveremote/layouts.read"
});
console.log(await resp.json());
```

Both return a JSON object containing `access_token`. Set it as `Authorization: Bearer <token>`
in the request headers.

If you don't know the token endpoint URL, discover it from the OIDC metadata document:
`https://cognito-idp.us-east-2.amazonaws.com/us-east-2_65NKvrlha/.well-known/openid-configuration` — look for the `token_endpoint` field.

## API integration tests

Tests use a `TestJwtAuthority` (`test/AdaptiveRemote.Backend.ApiTests/Support/TestJwtAuthority.cs`),
a minimal local OIDC/JWKS server started per scenario. The service is configured with
`Cognito__Authority` pointing at this server, so JWT validation runs end-to-end without a
real Cognito user pool. See `AuthenticationEndpoints.feature` for the test scenarios.
