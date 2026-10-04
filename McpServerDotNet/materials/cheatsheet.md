# Cheat sheet: MCP servers in .NET

> For the presenter: learn it before the Session, keep it open during Q&A. The Script (`script.md`) links here.
> Every concept has three layers: **Say** (1–2 sentences you can say out loud), **Analogy** (to make it stick), **In our demo** (where to point).
> Facts checked against the MCP specification 2026-07-28 and the Keycloak docs on 2026-10-04.

Contents: [Project architecture](#project-architecture) · [OAuth in MCP, step by step](#oauth-in-mcp-step-by-step) · [Keycloak](#keycloak) · [Acronyms](#acronyms) · [What's next](#whats-next)

## Project architecture

**Say:** "One F# process with two adapters, REST and MCP, over one pure domain and one in-memory store. Aspire starts the API, and Keycloak in the OAuth profile."

**Analogy:** A shop with two doors (REST for programs, MCP for AI agents) and one back office. Both doors lead to the same back office and the same rules.

**In our demo:** hidden slide A2 (`slides.html?showHiddenSlides=true#/a2`), README section "Architecture" (Mermaid diagram).

### Request path

1. A client sends a request: `logistics.http` calls REST, while VS Code and MCP Inspector call `POST /mcp` (JSON-RPC).
2. Only for `/mcp`: `McpAuth.fs` checks the caller, depending on the mode (`None`: everyone passes; `OAuth`: a valid Keycloak token is required).
3. The adapter (`Rest.fs` or `Mcp.fs`) turns the request into a domain call.
4. `Domain.fs` checks the 5 rules. It is pure F#: no HTTP, no database, no time calls, so it is easy to test.
5. `Store.fs` saves the new Dispatch board (one immutable value, swapped under a lock).

### Where to find what

| If someone asks… | Open |
|---|---|
| "Where are the rules?" | `Logistics.Api/Domain.fs`; plain-English version: resource `logistics://rules` in `Mcp.fs` |
| "How does the model know what a tool does?" | `Mcp.fs`: `[<McpServerTool(Name = ...)>]` + `Description(...)`. The description is the only manual the model gets. |
| "How do you register the MCP server?" | `Program.fs`: `AddMcpServer().WithHttpTransport(...).WithToolsFromAssembly()`, then `MapMcp("/mcp")` in `McpAuth.fs` |
| "How is it protected?" | `McpAuth.fs`: `AddJwtBearer` (checks `iss`, `aud`, signature) + `AddMcp` (401 challenge and PRM). `dispatch_order` has `[<Authorize(Roles = "dispatcher")>]`. |
| "Where does `dispatched by alice` come from?" | `Dispatcher.fs`: `preferred_username` claim from the token, else `anonymous` |
| "What is the data?" | `Seed.fs`; the comment at the top lists tomorrow's Dispatches and traps |
| "How are profiles switched?" | `McpServerDotNet.AppHost/AppHost.cs` + `Properties/launchSettings.json` (`None`, `ApiKey`, `OAuth`) |
| "Where are users and roles?" | `McpServerDotNet.AppHost/Realms/mcp-realm.json` |
| "Is it tested?" | `Logistics.Api.Tests/`: `Domain/` (rules), `Adapters/` (REST, MCP, OAuth over HTTP), `DemoScenes/` (the demo files still match the API) |

### Design choices people ask about

- **Why in-process, not a separate MCP service?** The MCP adapter reuses the domain directly: no extra network hop, and REST shapes don't leak into the tools. Trade-off: you need access to the API code (slide 7).
- **Why stateless?** The server keeps no session between calls (`HttpServerSessionMode.Stateless`). Our tools don't need one, and the 2026-07-28 spec removed protocol sessions anyway.
- **Why F# for the API and C# for the AppHost?** The MCP C# SDK works from F# (use `task {}`). The Aspire AppHost is C#-first, so F# would only add friction there.

## OAuth in MCP, step by step

This is slide 10. First the three roles:

| Role (OAuth name) | In our demo | Job |
|---|---|---|
| **Client** | VS Code, MCP Inspector | Wants to call the MCP server for a user |
| **Authorization Server (AS)** | Keycloak | Logs the user in and issues tokens |
| **Resource Server / Protected Resource (RS)** | our MCP server at `http://localhost:5080/mcp` | Accepts or rejects tokens |

**Say:** "Plain OAuth already gives us login and tokens. MCP adds three things so that any client can work with any server without manual setup: the server tells the client where to log in (PRM), the token is valid only for this server (audience), and the login is protected by PKCE."

### The 7 steps on the slide

1. **Client → MCP server: `POST /mcp` without a token.** The client knows only the server URL.
2. **Server → client: `401 Unauthorized`** with the header `WWW-Authenticate: Bearer resource_metadata="http://localhost:5080/.well-known/oauth-protected-resource/mcp"`. In other words: "You need a token. Read this address to find out how to get one."
3. **(red, MCP) Client reads the PRM.** The JSON says: `authorization_servers: ["http://localhost:8080/realms/mcp"]`, `scopes_supported: ["mcp:tools"]`. Now the client knows that Keycloak issues the tokens.
4. **Client reads the AS metadata** at `http://localhost:8080/realms/mcp/.well-known/openid-configuration`: where the login page is, where the token endpoint is, and that S256 PKCE is supported.
5. **(red, MCP) Browser login with PKCE and `resource`.** The user logs in (alice or bob). The request carries a PKCE `code_challenge` and `resource=http://localhost:5080/mcp`.
6. **(red, MCP) Keycloak returns an access token** with `aud = http://localhost:5080/mcp` (and `preferred_username`, `roles`).
7. **Client → server: `POST /mcp` + `Authorization: Bearer <token>`.** The server checks the signature, `iss` (Keycloak) and `aud` (itself). For `dispatch_order` it also checks the role `dispatcher`.

### PRM: Protected Resource Metadata

- **Expansion:** Protected Resource Metadata, RFC 9728.
- **Say:** "A small JSON file that the MCP server publishes about itself: who issues tokens for me and which scopes I understand. The 401 points to it, so the client finds the login server by itself. No configuration."
- **Analogy:** A locked door with a sign: "Keys are issued at reception, 2nd floor." You don't need to know the building in advance.
- **In our demo:** `logistics.http`, scene 6: `POST /mcp` → 401 → `GET /.well-known/oauth-protected-resource/mcp`. Code: `AddMcp(... ProtectedResourceMetadata(...))` in `McpAuth.fs`.
- **Spec facts:** MCP servers MUST implement PRM. In MCP, `authorization_servers` is required (in the RFC it is optional). Clients MUST understand both the header and the well-known URL.

### Audience binding (`resource` and `aud`)

- **Expansion:** `aud` = audience, a standard JWT claim (RFC 7519). `resource` = Resource Indicators parameter (RFC 8707).
- **Say:** "Every token is stamped with the server it is for. Our server rejects a token made for another server, and another server rejects ours. A stolen token is useless somewhere else."
- **Analogy:** A concert ticket with the venue and date printed on it. It doesn't work at a different concert, even if it's real.
- **In our demo:** the token has `aud = http://localhost:5080/mcp`; `ValidAudience = resource` in `McpAuth.fs`.
- **Spec facts:** clients MUST send `resource` in the authorization request and the token request. Servers MUST check the audience. Servers MUST NOT pass the client's token on to other APIs ("token passthrough").
- **Honest detail (if someone knows Keycloak well):** Keycloak ignores the `resource` parameter by default (support is an experimental feature, `--features=resource-indicators`). So in our realm the `aud` value comes from an **Audience mapper** on the client scope `mcp:tools`. That is the approach Keycloak's own MCP guide documents. The result is the same: the token works only for our server.

### PKCE

- **Expansion:** Proof Key for Code Exchange, pronounced "pixie", RFC 7636.
- **Say:** "Login in the browser ends with a one-time code, and the app exchanges this code for a token. PKCE proves that the app exchanging the code is the same app that started the login. So a stolen code is useless."
- **Analogy:** A cloakroom. When you leave your coat (start the login), you keep a ticket half (the secret). To get the coat back (the token), you must show the matching half. Someone who only saw the coat number can't take it.
- **How it works:**
  1. The client makes a random secret, the `code_verifier` (43–128 characters).
  2. It sends only its hash, `code_challenge = BASE64URL(SHA256(code_verifier))`, with the login request. **S256** is the name of this hash method.
  3. After login it sends the real `code_verifier` with the token request.
  4. Keycloak hashes it and compares the result with the challenge. If they match, it issues the token.
- **Why it matters here:** VS Code and Inspector are **public clients**: they run on your machine and can't keep a client secret. PKCE replaces the secret.
- **Spec facts:** clients MUST use PKCE with S256. If the AS metadata doesn't list `code_challenge_methods_supported`, the client MUST stop. `plain` (sending the secret itself) is not enough, because anyone who sees the first request also sees the secret.
- **In our demo:** both Keycloak clients have `pkce.code.challenge.method = S256` in `mcp-realm.json`. Inspector's "OAuth step by step" walks through the same steps one by one.

### Authentication vs authorization (scene 6)

- **Authentication** = who are you? bob logs in successfully, so the read-only tools work.
- **Authorization** = what may you do? bob has no role `dispatcher`, so `dispatch_order` is not in his `tools/list`. If he calls it by name anyway, he gets the JSON-RPC error `-32600`.

### Other questions about auth

- **"Why is it http, not https?"** Demo only. The spec says all authorization server endpoints MUST use HTTPS, and redirect URIs must be `localhost` or HTTPS (slide 11).
- **"Why not just an API key?"** It's possible (profile `ApiKey`, header `X-Api-Key`, see README). But a key is one shared secret: no per-user identity (every Dispatch says `api-key`), no roles, no discovery, no audience. It's fine for service-to-service; for a shared remote MCP server, OAuth.
- **"What about scopes?"** `mcp:tools` is a scope: a named permission that the client asks for. If a token lacks a scope, the spec says the server SHOULD answer `403` with `insufficient_scope`, and the client asks for more.
- **"Is OAuth 2.1 a standard?"** It's still an IETF draft that combines OAuth 2.0 with current best practice (for example, PKCE for everyone, no implicit flow). MCP builds on it.
- **"Entra ID?"** Same protocol, different configuration. Details are in the Script, section 10.

## Keycloak

**Say:** "Keycloak is an open-source identity and access management server, a CNCF incubating project. Here it's the Authorization Server: it logs users in and issues tokens. Aspire runs it as a container."

**Analogy for the whole thing:** Keycloak is a building's security office. A **realm** is one company in the building with its own staff list. **Users** are the staff. **Roles** are what is written on their badges. **Clients** are the doors and apps that ask security to check a badge. **Client scopes** and **mappers** decide what's printed on the badge (the token).

| Concept | Say | In our demo (`mcp-realm.json`) |
|---|---|---|
| **Realm** | An isolated space with its own users, clients, roles and login page. A user belongs to and logs into one realm. The issuer URL contains the realm name. | Realm `mcp`; issuer `http://localhost:8080/realms/mcp` |
| **User** | Someone who can log in. | `alice` / `alice`, `bob` / `bob` |
| **Realm role** | A role valid in the whole realm, e.g. a job function. (A **client role** belongs to one client only.) | `dispatcher` (alice has it, bob doesn't) |
| **Client** | An application that asks Keycloak to log a user in. | `vscode`, `mcp-inspector` |
| **Public vs confidential client** | Confidential clients have a secret (e.g. a backend). Public clients can't keep one (desktop, browser, CLI), so they rely on PKCE and exact redirect URIs. | Both clients are public, PKCE S256 |
| **Redirect URI** | Where Keycloak sends the browser back after login. It must match exactly, so a fake app can't receive the code. | Inspector: `http://127.0.0.1:6274/oauth/callback` |
| **Client scope** | A reusable bundle of claims and permissions that a client can request with the OAuth `scope` parameter. | `mcp:tools` (+ `offline_access` for refresh tokens) |
| **Protocol mapper** | A rule that puts a value into the token. | On `mcp:tools`: Audience mapper (`aud` = MCP URL), `preferred_username`, `sub`, and a realm roles mapper (flat `roles` claim) |
| **SSO session** | After one login, Keycloak remembers you in the browser, so the next app logs in without a password. | Why the Script says to log bob out before alice logs in |
| **Identity brokering** | Keycloak can send the login to another provider (Google, Entra ID, another OIDC or SAML server). | Not used; a good answer to "can it use our company login?" |

**Gotchas from the spike (if asked "was it hard?"):** Inspector uses `127.0.0.1`, not `localhost`, in its redirect URI and asks for `offline_access`. Importing custom client scopes removes Keycloak's built-in scopes, so the mappers we need are defined on `mcp:tools`. Keycloak imports the realm only when the container is created.

## Acronyms

| Acronym | Expansion | One sentence |
|---|---|---|
| **MCP** | Model Context Protocol | An open protocol that lets AI apps use tools, data and prompts from external servers in a standard way. |
| **JSON-RPC** | JSON Remote Procedure Call | A simple protocol where each message names a method and its parameters in JSON; MCP uses version 2.0. |
| **stdio** | standard input/output | MCP transport where the host starts the server as a local process and talks through its stdin/stdout. |
| **HTTP / HTTPS** | Hypertext Transfer Protocol (Secure) | The web protocol; HTTPS adds encryption with TLS. |
| **SSE** | Server-Sent Events | A one-way stream of events from server to client over HTTP; Streamable HTTP may use it for responses. |
| **SDK** | Software Development Kit | A library for a platform; here the MCP C# SDK (`ModelContextProtocol.AspNetCore`). |
| **REST** | Representational State Transfer | The usual style of HTTP APIs: resources at URLs, HTTP verbs (`GET`, `POST`, `DELETE`). |
| **API** | Application Programming Interface | The contract that lets one program call another. |
| **URI / URL** | Uniform Resource Identifier / Locator | A URI names a thing (`logistics://rules`); a URL is a URI that also tells you where to fetch it (`http://…`). |
| **IAM** | Identity and Access Management | Managing who users are and what they may access; Keycloak and Entra ID are IAM systems. |
| **SSO** | Single Sign-On | Log in once and use many apps without logging in again. |
| **OAuth** | Open Authorization | A standard for giving an app limited access on a user's behalf by tokens, without sharing the password. |
| **OIDC** | OpenID Connect | An identity layer on top of OAuth 2.0: adds who the user is (ID token) and standard discovery. |
| **AS** | Authorization Server | The OAuth role that logs users in and issues tokens (Keycloak). |
| **RS** | Resource Server (Protected Resource) | The OAuth role that accepts tokens and serves data (our MCP server). |
| **PRM** | Protected Resource Metadata | RFC 9728: JSON where a server says which AS issues its tokens; the 401 points to it. |
| **PKCE** | Proof Key for Code Exchange | RFC 7636: proves the app that redeems the login code is the app that started the login. |
| **S256** | SHA-256 code challenge method | The PKCE variant where the challenge is a SHA-256 hash of the secret; required by MCP. |
| **SHA** | Secure Hash Algorithm | A family of one-way hash functions; SHA-256 gives a 256-bit fingerprint. |
| **JWT** | JSON Web Token | RFC 7519: a signed set of claims, the format of our access tokens; say "jot". |
| **JWKS** | JSON Web Key Set | The public keys an AS publishes so servers can check token signatures. |
| **iss / sub / aud** | issuer / subject / audience | JWT claims: who issued the token, who the user is, which server it is for. |
| **RFC** | Request for Comments | The numbered documents that define internet standards (IETF). |
| **IETF** | Internet Engineering Task Force | The body that publishes RFCs and drafts such as OAuth 2.1. |
| **DCR** | Dynamic Client Registration | RFC 7591: a client registers itself at the AS by an API call; deprecated in MCP 2026-07-28. |
| **CIMD** | Client ID Metadata Document | The client ID is an HTTPS URL to a JSON document about the client; the AS reads it. Preferred in MCP. |
| **RBAC** | Role-Based Access Control | Permissions go to roles, roles go to users (`dispatcher` → alice). |
| **CNCF** | Cloud Native Computing Foundation | The Linux Foundation home of Kubernetes, OpenTelemetry and Keycloak. |
| **OTel** | OpenTelemetry | The vendor-neutral standard for traces, metrics and logs; Aspire's dashboard shows them. |
| **MRTR** | Multi Round-Trip Requests | New in 2026-07-28: instead of calling the client, the server answers "input required" and the client retries with the answer. |
| **SEP** | Specification Enhancement Proposal | A numbered proposal to change the MCP spec (e.g. SEP-2577 deprecated Sampling and Roots). |
| **ADR** (domain) | *Accord … Dangereuses par Route*: European Agreement on the International Carriage of Dangerous Goods by Road | The UN agreement for dangerous goods on roads; drivers need an ADR certificate. **Not** "Architecture Decision Record" (the repo's `docs/adr/`). |
| **CPC** (domain) | Driver Certificate of Professional Competence | EU qualification every professional truck or bus driver needs on top of the licence, renewed with periodic training. |
| **C+E** (domain) | EU driving licence category C plus E | A heavy truck (over 3.5 t) with a heavy trailer (over 750 kg): what a tractor unit + trailer needs. |
| **TMS / WMS** | Transportation / Warehouse Management System | Business software for planning transport / running a warehouse; our demo domain is a tiny slice of a TMS. |

## What's next

This is slide 13. Big news first, because someone may ask: **the MCP spec 2026-07-28 deprecated Sampling and Roots** (and Logging). "Deprecated" means they still work, but new implementations should not add them, and they will be removed after at least 12 months. Also, servers no longer send requests to clients; they use MRTR (see below).

### Prompts (server primitive)

- **Say:** "Prompt templates that the server offers and the **user** picks, often as slash commands. The server writes the good prompt once; everybody reuses it."
- **Analogy:** Saved replies in your email.
- **Logistics example:** `/plan-tomorrow` → "Plan all open Transport Orders for tomorrow using the attached rules" + `logistics://rules` attached automatically.
- **Compare:** tools are picked by the model, resources are attached by the user/app, prompts are picked by the user (slide 5).

### Elicitation (client feature, active)

- **Say:** "The server can stop in the middle of a call and ask the user for missing information, through the client's UI."
- **Analogy:** A waiter who comes back to ask: "Fries or salad with that?"
- **Logistics example:** `dispatch_order` for ORD-106 without a trailer: the server finds two free Curtainsiders and asks "TRL-01 or TRL-02?". The user picks one in a small form in VS Code.
- **Two modes:** **form** (a simple form, defined by JSON Schema) and **URL** (opens a web page, for things like payments or third-party login). Servers MUST NOT ask for passwords, API keys or tokens in form mode.
- **The user can always say no:** answers are `accept`, `decline` or `cancel`.

### Sampling (client feature, deprecated)

- **Say:** "The server borrows the client's model: it asks the client to run an LLM prompt, so the server needs no own AI key. Deprecated in 2026-07-28: servers should call an LLM provider directly instead."
- **Logistics example:** the server asks the model "Write a short note for the driver about ORD-103".

### Roots (client feature, deprecated)

- **Say:** "The client told the server which folders the user is working in, as a hint, not as security. Deprecated in 2026-07-28: pass paths as tool parameters or config instead."
- **Example:** a file-system MCP server learns that you work in `~/repos/logistics`.

### MRTR: how the server asks the client now

- **Say:** "Before, the server sent its own request to the client in the middle of a call. Now the server answers with 'input required', the client collects the answer (for example from the user) and sends the original request again with the answer."
- **Analogy:** An online form that says "missing field: phone number" and you submit it again, instead of someone calling you.

### Client registration: pre-registered, CIMD, DCR

The problem: before a client can log in, the authorization server must know the client (its ID and redirect URIs). In our demo we created `vscode` and `mcp-inspector` in Keycloak by hand. A public MCP server can't do that for every app in the world.

| Way | How it works | Status in MCP 2026-07-28 |
|---|---|---|
| **Pre-registration** | An admin creates the client in the AS in advance. | Fine when you know your clients (our demo, a company tool) |
| **CIMD**, Client ID Metadata Document | The client's ID **is** an HTTPS URL, e.g. `https://client.example.com/oauth/metadata.json` (made-up example). The AS downloads this JSON (name, redirect URIs) and trusts it. No registration call, and the same ID works with every AS. | Preferred (SHOULD) |
| **DCR**, Dynamic Client Registration | The client calls the AS's registration API and gets a new client ID. Every AS creates new entries; hard to control. | Deprecated, only for backward compatibility |

- **Say:** "With CIMD the client ID is a web address of the client's own description. Keycloak reads it like a business card. So a public MCP server can accept VS Code without anyone registering it."
- **Analogy:** Pre-registration = a guest list at the door. DCR = everyone fills in a new form at the door. CIMD = you show an official ID card that the door can check online.
- **Keycloak:** supports DCR; CIMD is experimental (`--features=cimd`).
- **Client order in the spec:** pre-registered ID → CIMD → DCR → ask the user.
