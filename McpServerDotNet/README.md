# MCP server in .NET (F#)

An MCP server in F# over a simplified logistics API, run with Aspire and used from GitHub Copilot in VS Code and MCP Inspector.

**Stack:** .NET 10, F# (API, domain, MCP via `ModelContextProtocol.AspNetCore` 2.2.0), C# Aspire AppHost 13.6.0, xUnit v3 + Unquote.

## What's inside

| Path | What it shows |
|------|---------------|
| `McpServerDotNet.AppHost/` | C# Aspire AppHost. One launch profile per auth mode (`None` for now). |
| `McpServerDotNet.ServiceDefaults/` | C# Aspire ServiceDefaults: OpenTelemetry, health checks (`/health`, `/alive`). |
| `Logistics.Api/` | F# API (resource `logistics-api`): domain, seed, REST adapter (`Rest.fs`), MCP adapter (`Mcp.fs`). |
| `.vscode/mcp.json` | MCP server entry for VS Code / GitHub Copilot. |
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

Tests:

```sh
dotnet test
```

## Sessions

| Date | Audience | Notes |
|------|----------|-------|
| | Internal team | |
