# MCP server in .NET (F#)

An MCP server in F# over a simplified logistics API, run with Aspire and used from GitHub Copilot in VS Code and MCP Inspector.

**Stack:** .NET 10, F# (API, domain, MCP via `ModelContextProtocol.AspNetCore` 2.2.0), C# Aspire AppHost 13.6.0, xUnit v3 + Unquote.

## What's inside

| Path | What it shows |
|------|---------------|
| `McpServerDotNet.AppHost/` | C# Aspire AppHost. One launch profile per auth mode: `None`, `ApiKey`, `OAuth` (stub for now). |
| `McpServerDotNet.ServiceDefaults/` | C# Aspire ServiceDefaults: OpenTelemetry, health checks (`/health`, `/alive`). |
| `Logistics.Api/` | F# API (resource `logistics-api`): domain, seed, REST adapter (`Rest.fs`), MCP adapter (`Mcp.fs`), auth toggle for `/mcp` (`McpAuth.fs`). |
| `.vscode/mcp.json` | MCP server entries for VS Code / GitHub Copilot: `logistics` (no key) and `logistics-api-key` (`ApiKey` mode). |
| `Logistics.Api.Tests/` | F# tests: `Domain/`, `DemoScenes/`, `Adapters/`. |
| `materials/slides.html` | Slides. Open in a browser and press `S` for the speaker view. Works offline. |
| `materials/script.md` | Presenter's Script. |

## Run it

Requires the .NET 10 SDK.

```sh
cd McpServerDotNet
dotnet run --project McpServerDotNet.AppHost --launch-profile None
```

The dashboard opens at http://localhost:15080 (login link in the console). The API listens on http://localhost:5080; `GET /health` returns `Healthy`.

REST (open in every mode): `GET /drivers`, `/drivers/{id}`, `/tractors`, `/trailers`, `/orders` (`?status=open`), `/orders/{id}`, `/orders/{id}/dispatch-options`, `/dispatches`; `POST /dispatches` (body `{orderId, driverId, tractorId, trailerId}`; 201, or 404/409 with the reason as plain text), `DELETE /dispatches/{id}`. Data is in memory and reseeded on every start, with dates relative to startup (see `Logistics.Api/Seed.fs`).

MCP (Streamable HTTP, stateless) on http://localhost:5080/mcp, same process: read-only tools `get_drivers`, `list_open_orders`, `find_dispatch_options`, `get_driver_schedule`, the mutating tool `dispatch_order` (rule violations come back as tool text with `isError: true`; success names the Dispatcher) and the resource `logistics://rules`. VS Code picks the server up from `.vscode/mcp.json` (open the `McpServerDotNet` folder). MCP Inspector: `npx @modelcontextprotocol/inspector`, transport Streamable HTTP, URL above.

### Auth modes

`McpAuth:Mode` (`None | ApiKey | OAuth`) protects `/mcp` only; REST stays open. Pick the mode with the AppHost launch profile and restart to switch (data reseeds).

| Profile | `/mcp` | Dispatcher in `dispatch_order` |
|---------|--------|-------------------------------|
| `None` | open | `anonymous` |
| `ApiKey` | needs header `X-Api-Key` (401 without it) | `api-key` |
| `OAuth` | not built yet: the API refuses to start | |

`ApiKey`: the key is the Aspire secret parameter `mcp-api-key`. Set it once in the AppHost user secrets, or leave it unset and enter it in the dashboard when Aspire asks:

```sh
dotnet user-secrets --project McpServerDotNet.AppHost set Parameters:mcp-api-key <key>
dotnet run --project McpServerDotNet.AppHost --launch-profile ApiKey
```

In VS Code start the `logistics-api-key` server; it prompts for the key (password field, not stored in the repo) and sends it as `X-Api-Key`. In MCP Inspector add the header `X-Api-Key` under Authentication. One shared key: no per-user identity, no roles.

Tests:

```sh
dotnet test
```

## Sessions

| Date | Audience | Notes |
|------|----------|-------|
| | Internal team | |
