# Spike: MCP OAuth with Keycloak in Aspire

Throwaway spike for the ticket "Spike: OAuth flow with VS Code and MCP Inspector against Keycloak in Aspire" (map: "Map: prepare four NDC Oslo knowledge sharing Topics"). Run 2026-10-02.

## Setup

- `McpServer/` (F#): `ModelContextProtocol.AspNetCore` 2.2.0, one tool `whoami` that returns the caller's claims. `McpAuth:Enabled` switches `AddJwtBearer` + `.AddMcp(...)` + `RequireAuthorization()` on or off.
- `AppHost/` (C#): Aspire 13.6.0, `Aspire.Hosting.Keycloak` 13.6.0-preview (image `keycloak:26.6`), port 8080, realm import from `Realms/mcp-realm.json`. Keycloak is added only when `McpAuth:Enabled=true`.
- Realm `mcp`: user `alice`/`alice`; client scope `mcp:tools` with an Audience mapper (`aud` = `http://localhost:5080/mcp`); public PKCE clients `vscode` and `mcp-inspector`.
- `.vscode/mcp.json`, `inspector.json`: client configs. `scripts/oauth_flow.py`: walks the flow without a browser.

Run: `dotnet run --project AppHost` and then connect a client to `http://localhost:5080/mcp`.

## Results

| Client | Result |
|---|---|
| Script (`vscode`, `mcp-inspector`) | 401 → PRM → AS metadata → PKCE + `resource` → token → `whoami` = alice |
| GitHub Copilot in VS Code (`oauth.clientId: vscode`) | Works. Only the browser login; no extra prompt. |
| MCP Inspector 2.9.0 web | Works after the two realm fixes below. |
| Auth off | PRM 404; `whoami` = anonymous |

## Gotchas found

1. **Inspector web redirect URI is `http://127.0.0.1:6274/oauth/callback`**, not `localhost`. Keycloak matches exactly, so register both (+ `webOrigins`).
2. **Inspector requests `offline_access`** (refresh token on by default; Keycloak advertises it). The realm needs the `offline_access` client scope as optional on the client, and the user needs the `offline_access` realm role. Otherwise you get `invalid_scope` and then `Offline tokens not allowed`.
3. **A realm import with its own `clientScopes` replaces Keycloak's built-in scopes** (`basic`, `profile`, `offline_access`). Without extra mappers the token has no `sub` or `preferred_username`. Imported users also get no default roles.
4. **`ClaimsPrincipal` tool parameter fails when auth is off** ("No service of the requested type was found"). Mark it `[<Optional; DefaultParameterValue(null: ClaimsPrincipal)>]`.
5. The SDK's 401 challenge has no `scope` parameter (spec: SHOULD). Clients fall back to PRM `scopes_supported`; both clients handled that.
6. Keycloak on http works for both clients and returns `iss` on the redirect (RFC 9207). Keycloak ignores the `resource` parameter; the Audience mapper sets `aud`. Browsers send Keycloak's `Secure` cookies on `localhost`; non-browser HTTP clients may not.
7. `WebApplication.CreateBuilder()` without args ignores `--urls`.
