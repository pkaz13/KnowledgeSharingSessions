# Research: authentication options for an MCP server in .NET

Ticket: #6 (parent map #4). Researched 2026-10-02 against primary sources.
Context: McpServerDotNet demo = logistics REST API + MCP server in one ASP.NET Core process, Streamable HTTP, Aspire AppHost; clients = GitHub Copilot in VS Code + MCP Inspector; auth must be demoable and easy to toggle.

## TL;DR

- **Recommended demo setup:** Keycloak container via `Aspire.Hosting.Keycloak` + realm import JSON; API uses `AddJwtBearer` + SDK's `.AddMcp(...)` auth handler (serves Protected Resource Metadata, emits `WWW-Authenticate`); `MapMcp().RequireAuthorization()` only when a flag is on.
- **Client registration for the demo:** pre-register public clients in the realm JSON (VS Code: redirect URIs `http://127.0.0.1:33418` + `https://vscode.dev/redirect`; Inspector web: `http://localhost:6274/oauth/callback`). Avoids depending on experimental Keycloak CIMD or anonymous-DCR policy tweaks. Fallbacks: anonymous DCR (deprecated in spec but still supported by VS Code, Inspector, Keycloak) or CIMD (`--features=cimd`, experimental, needs container internet access to fetch `https://vscode.dev/oauth/client-metadata.json`).
- **Toggle:** one boolean (e.g. `Mcp:Auth:Enabled`) read by AppHost and API; AppHost adds the Keycloak resource + env var only when on. Off = plain unauthenticated MCP endpoint (spec-legal: auth is OPTIONAL).
- **Simplest alternative:** static bearer/API-key header via `headers` in `.vscode/mcp.json` with `${input:...}` password prompt, and `--header`/per-server `headers` in Inspector. Works everywhere, zero AS, but is **not** the MCP authorization spec (no discovery, no audience binding, no consent); fine as a "step 1" slide.
- Not verified hands-on: an end-to-end run of VS Code / Inspector against Keycloak 26.8 on `http://localhost`. Everything below is from spec text and source code; a 30-min spike is advised before building slides.

## 1. What the MCP spec requires (current revision 2026-07-28)

Source: https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization (served at `/specification/latest/...` on 2026-10-02) and sub-pages.

- **Auth is OPTIONAL.** "Authorization is **OPTIONAL** for MCP implementations." When supported, HTTP transports **SHOULD** conform; stdio **SHOULD NOT** (use environment credentials).
- **Roles:** MCP server = OAuth 2.1 resource server; client = OAuth 2.1 client; AS is separate (or co-hosted) and out of scope.
- **Protected Resource Metadata (RFC 9728) is MUST** for servers: PRM document **MUST** include `authorization_servers` (≥1). Server advertises it via `WWW-Authenticate: Bearer resource_metadata="..."` on 401 and/or well-known URI (`/.well-known/oauth-protected-resource/<mcp-path>` then root). Clients **MUST** support both. Source: `.../authorization/authorization-server-discovery`.
- **AS metadata:** AS **MUST** offer RFC 8414 or OIDC Discovery; clients **MUST** try both (path-insertion order defined) and **MUST** reject metadata whose `issuer` differs.
- **Client registration** (`.../authorization/client-registration`), client priority: (1) pre-registered client, (2) **Client ID Metadata Documents (CIMD)** if AS advertises `client_id_metadata_document_supported`, (3) **Dynamic Client Registration (RFC 7591)** if `registration_endpoint`, (4) prompt user. CIMD is **SHOULD**; DCR is **MAY** and **deprecated in 2026-07-28** (kept for backward compat). DCR clients **MUST** send `application_type` (`native` for localhost apps).
- **PKCE:** clients **MUST** use PKCE (S256) and **MUST** refuse if AS metadata lacks `code_challenge_methods_supported`.
- **Resource Indicators (RFC 8707):** clients **MUST** send `resource=<canonical MCP server URI>` on authorize and token requests.
- **Token validation:** server **MUST** validate tokens and that it is the intended audience; **MUST NOT** accept or pass through other tokens. Invalid/expired → 401; insufficient scope → 403 with `error="insufficient_scope"` (step-up).
- **Scopes:** server **SHOULD** put `scope` in the 401 challenge; else clients use PRM `scopes_supported`.
- **New in 2026-07-28** (changelog `.../2026-07-28/changelog`): RFC 9207 `iss` validation by clients (SEP-2468), `application_type` in DCR (SEP-837), credentials bound per issuer (SEP-2352), DCR deprecated (PR #2858). Also stateless protocol (no `initialize`, no `Mcp-Session-Id`) - unrelated to auth but affects demo code.
- **Communication security** (`.../authorization/security-considerations`): "All authorization server endpoints **MUST** be served over HTTPS"; redirect URIs must be `localhost` or HTTPS. A local Keycloak on `http://localhost:8080` technically violates this - acceptable for a demo, worth one sentence on the slide.

## 2. What the ModelContextProtocol .NET SDK provides

Source: https://github.com/modelcontextprotocol/csharp-sdk (main @ c40ee04, 2026-09-18). Latest release **v2.2.0** (2026-08-13); v2.0.0 shipped 2026-07-28 alongside the spec; code references protocol `2026-07-28`.

- **Package `ModelContextProtocol.AspNetCore`, namespace `ModelContextProtocol.AspNetCore.Authentication`:**
  - `AuthenticationBuilder.AddMcp(options => ...)` registers `McpAuthenticationHandler` (scheme `McpAuthenticationDefaults.AuthenticationScheme`). It forwards authenticate to `"Bearer"` (JwtBearer), serves PRM at `/.well-known/oauth-protected-resource/<resource-path>` (resource derived from request URL, no trailing slash) and on challenge writes `WWW-Authenticate: Bearer resource_metadata="..."`.
  - `McpAuthenticationOptions.ResourceMetadata` (`ProtectedResourceMetadata`: `AuthorizationServers`, `ScopesSupported`, `ResourceDocumentation`, ...), `ResourceMetadataUri` override, `Events` (customize PRM per request).
  - Files: `src/ModelContextProtocol.AspNetCore/Authentication/McpAuthentication{Extensions,Handler,Options,Defaults,Events}.cs`.
- **Token validation is plain ASP.NET Core:** `AddJwtBearer` with `Authority`, `ValidAudience = <MCP server URL>`, `ValidIssuer`. SDK does not issue tokens.
- **Authorization in tools:** `ClaimsPrincipal` parameter injected into tools (not in schema); `[Authorize]`, `[Authorize(Roles=...)]`, `[AllowAnonymous]` on tools/prompts/resources after `.AddAuthorizationFilters()`; unauthorized items are filtered from `tools/list`. Source: `docs/concepts/identity/identity.md`. Nice demo beat: show the caller name inside a tool result.
- **Sample:** `samples/ProtectedMcpServer/Program.cs` - exact pattern to copy:
  ```csharp
  builder.Services.AddAuthentication(o => {
      o.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
      o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme; })
    .AddJwtBearer(o => { o.Authority = authUrl; o.TokenValidationParameters = new() { ValidAudience = serverUrl, ValidIssuer = authUrl, ... }; })
    .AddMcp(o => o.ResourceMetadata = new() { AuthorizationServers = { authUrl }, ScopesSupported = ["mcp:tools"] });
  ...
  app.MapMcp().RequireAuthorization();
  ```
  Sample uses `HttpServerSessionMode.Stateless` (default).
- **Client side:** `ClientOAuthOptions` / `ClientOAuthProvider` in `ModelContextProtocol.Core` support DCR, CIMD (`ClientMetadataDocumentUri`), token cache; `samples/ProtectedMcpClient`. Not needed for the demo (clients are VS Code / Inspector).
- **Test AS:** `tests/ModelContextProtocol.TestOAuthServer` - in-memory AS (RFC 8414 + OIDC metadata, `/authorize`, `/token`, `/register`, CIMD, enforces `resource`, auto-approves without login UI, HTTPS `localhost:7029`). Not a NuGet package; would need copying. Lightest possible AS, but no login screen = weaker demo story.

## 3. Cheapest local authorization server for Aspire

| Option | Effort | Notes |
|---|---|---|
| **Keycloak via `Aspire.Hosting.Keycloak`** (recommended) | Low | First-party Aspire integration (hosting pkg is preview). `builder.AddKeycloak("keycloak", 8080).WithRealmImport("./Realms")`; default image `quay.io/keycloak/keycloak:26.6`; random admin password in `Parameters:keycloak-password` unless supplied. Client pkg `Aspire.Keycloak.Authentication` → `AddKeycloakJwtBearer(serviceName, realm, o => o.Audience = ...)`, `RequireHttpsMetadata = false` in dev. Source: https://aspire.dev/integrations/security/keycloak/. Latest Aspire 13.6.0 (2026-09-29); latest Keycloak 26.8.0 (2026-10-01). |
| SDK TestOAuthServer (copied) | Low-Med | No container, no login UI, test-grade. |
| Entra ID | Med (needs tenant) | VS Code has its own Entra client registration and brokered sign-in; not local. |
| Others (Duende, OpenIddict, Zitadel, Authentik) | Med+ | No first-party Aspire hosting integration found; not evaluated. |

**Keycloak MCP specifics** (https://www.keycloak.org/securing-apps/mcp-authz-server, nightly 26.8 docs):
- Supports OAuth 2.1, RFC 8414/OIDC discovery, RFC 9207, RFC 7591 DCR, PKCE. MCP 2025-03-26 "fully supported"; 2025-06-18 / 2025-11-25 / 2026-07-28 "experimental".
- **Audience:** Keycloak doesn't put the MCP URL in `aud` by default. Option A: client scope with **Audience mapper** = MCP server URL (stable, recommended). Option B: `--features=resource-indicators` (experimental) + allow-list.
- **CIMD:** `--features=cimd` (experimental) + client policy/profile with `client-id-metadata-document` executor; for VS Code: trusted domains `vscode.dev`, `127.0.0.1`, `code.visualstudio.com`, "Restrict same domain" OFF.
- **Anonymous DCR** (for Inspector/VS Code): adjust realm Client Registration policies - Trusted Hosts (host sending the request), Allowed Client Scopes, Allowed Registration Web Origins (CORS, because Inspector runs JS).
- Feature flags with Aspire: likely `.WithArgs("--features=cimd,resource-indicators")` on the Keycloak resource - **not verified**; also Aspire's default image tag (26.6) may lag; pin with `.WithImageTag("26.8")` if CIMD/2026-07-28 behaviour matters.

## 4. Do the clients complete the OAuth flow?

### GitHub Copilot in VS Code
Source: VS Code source `src/vs/workbench/api/browser/mainThreadAuthentication.ts`, `src/vs/base/common/oauth.ts` (main, fetched 2026-10-02); docs https://code.visualstudio.com/docs/agents/reference/mcp-configuration.
- Implements the spec flow for `type: "http"` servers: 401 → PRM → AS metadata → browser sign-in.
- Registration order in code: cached/overridden client ID → **CIMD** if `client_id_metadata_document_supported` (client ID = `productService.authClientIdMetadataUrl`, i.e. `https://vscode.dev/oauth/client-metadata.json` per Den Delimarsky's post https://den.dev/blog/cimd-vs-code-mcp/) → **DCR** if `registration_endpoint` (public client, `application_type: "native"`, redirect URIs `https://insiders.vscode.dev/redirect`, `https://vscode.dev/redirect`, `http://127.0.0.1/`, `http://127.0.0.1:33418/`) → modal "Dynamic Client Registration not supported" asking for a client ID registered with `http://127.0.0.1:33418` and `https://vscode.dev/redirect`.
- Pre-registration in config: `"oauth": { "clientId": "..." }` on the server entry in `mcp.json` (docs).
- AS metadata URLs may be `http://` (code accepts `http://` and `https://`) - local Keycloak on http works from VS Code's side.
- Known gotcha: loopback port 33418 must be free, else redirect URI mismatch with exact-match AS (vscode issue #278512, via search; not re-verified).

### MCP Inspector (v2, latest 2.9.0, 2026-09-30)
Source: https://github.com/modelcontextprotocol/inspector `docs/mcp-server-configuration.md`, `docs/v1-to-v2-migration.md`, `clients/web/src/utils/oauthFlow.ts`.
- Full OAuth incl. DCR, CIMD, pre-registered `oauth.clientId`/`clientSecret`, scopes, extra authorize params (e.g. Keycloak `kc_idp_hint`), endpoint overrides, token revoke on clear. RFC 9207 issuer check enforced.
- Redirect URI: web UI `${origin}/oauth/callback` → default `http://localhost:6274/oauth/callback` (port `CLIENT_PORT`); CLI/TUI fixed `http://127.0.0.1:6276/oauth/callback`.
- v1's bearer-token sidebar field is gone; use per-server `headers` or `--header "Authorization: Bearer ..."`.
- Inspector has a "Protocol era" setting (`legacy`/`auto`/`modern`); with SDK v2 servers check which era is negotiated.

## 5. Simpler alternatives and spec trade-off

- **Static bearer / API key header.**
  - VS Code `.vscode/mcp.json`:
    ```json
    {
      "inputs": [{ "type": "promptString", "id": "mcp-token", "description": "MCP API token", "password": true }],
      "servers": { "logistics": { "type": "http", "url": "http://localhost:5080/mcp",
                   "headers": { "Authorization": "Bearer ${input:mcp-token}" } } }
    }
    ```
  - Inspector: per-server `headers` or `--header`.
  - Server: a tiny custom `AuthenticationHandler` (or middleware) comparing the header to a configured secret, then `RequireAuthorization()`.
  - Trade-off: spec makes auth optional, so this is legal, but it is **not** MCP authorization: no PRM/discovery, no per-user identity, no audience binding, no consent, secret sharing problem. Good for internal/dev; spec-compliant OAuth is the "real" answer.
- **Locally minted JWT** (`dotnet user-jwts create --audience ...`) + `AddJwtBearer` + header in mcp.json: real JWT validation and `ClaimsPrincipal` in tools without any AS; still no discovery flow. Middle ground if Keycloak spike fails.

## 6. Toggling auth on/off cleanly

Suggested shape (not prototyped):
- **AppHost (C#):** `var authEnabled = builder.Configuration.GetValue("McpAuth:Enabled", false);` (set in AppHost `appsettings.json`/launch profile, or `dotnet run -- --McpAuth:Enabled=true`). If on: add Keycloak (`AddKeycloak(...).WithRealmImport(...)`), `api.WithReference(keycloak).WaitFor(keycloak)`, and `api.WithEnvironment("McpAuth__Enabled", "true")`. Keeps the dashboard clean when off (no container). An Aspire `AddParameter` works too, but a parameter value can't easily decide whether a resource exists at model-build time; config is simpler.
- **API (F#/C#):** read `McpAuth:Enabled`; when true register `AddAuthentication().AddJwtBearer(...).AddMcp(...)` + `UseAuthentication/UseAuthorization`, and call `.RequireAuthorization()` on `MapMcp()` (and optionally REST endpoints). When false skip all of it.
- **Clients:** VS Code needs nothing extra - it reacts to the 401. Keep two server entries in `mcp.json` only if showing the static-header variant too.
- Demo beat: run with flag off → tools work; flip flag, restart AppHost → Copilot triggers browser login → tool shows the caller's name via `ClaimsPrincipal`.

## Open points / to verify in a spike
1. VS Code + Keycloak 26.x on `http://localhost:8080` with pre-registered client end-to-end (incl. `resource` param accepted/ignored, audience mapper).
2. Whether Aspire Keycloak `WithArgs("--features=...")` composes with the integration's own `start-dev --import-realm` args.
3. Keycloak issuer vs `Authority` under Aspire service discovery (`AddKeycloakJwtBearer` uses service name; issuer in token is the browser-facing `http://localhost:8080/realms/<realm>`).
4. SDK v2 / 2026-07-28 stateless protocol vs Copilot/Inspector protocol versions (separate from auth, but blocks the demo if mismatched).

## Sources
- MCP spec 2026-07-28: Authorization, Authorization Server Discovery, Client Registration, Security Considerations, Changelog - https://modelcontextprotocol.io/specification/2026-07-28/
- C# SDK repo - https://github.com/modelcontextprotocol/csharp-sdk (releases, `src/ModelContextProtocol.AspNetCore/Authentication`, `samples/ProtectedMcpServer`, `docs/concepts/identity/identity.md`, `tests/ModelContextProtocol.TestOAuthServer`)
- VS Code docs - https://code.visualstudio.com/docs/agents/reference/mcp-configuration ; source https://github.com/microsoft/vscode (`mainThreadAuthentication.ts`, `oauth.ts`)
- MCP Inspector - https://github.com/modelcontextprotocol/inspector (docs/mcp-server-configuration.md, docs/v1-to-v2-migration.md)
- Keycloak MCP guide - https://www.keycloak.org/securing-apps/mcp-authz-server ; releases https://github.com/keycloak/keycloak/releases
- Aspire Keycloak integration - https://aspire.dev/integrations/security/keycloak/
- Secondary (author is MCP auth spec maintainer): https://den.dev/blog/cimd-vs-code-mcp/
