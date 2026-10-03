# MCP server in .NET (F#)

An MCP server in F# over a simplified logistics API, run with Aspire and used from GitHub Copilot in VS Code and MCP Inspector.

**Stack:** .NET 10, F# (API, domain, MCP via `ModelContextProtocol.AspNetCore` 2.2.0), C# Aspire AppHost 13.6.0, xUnit v3 + Unquote.

## What's inside

| Path | What it shows |
|------|---------------|
| `McpServerDotNet.AppHost/` | C# Aspire AppHost. One launch profile per auth mode (`None` for now). |
| `McpServerDotNet.ServiceDefaults/` | C# Aspire ServiceDefaults: OpenTelemetry, health checks (`/health`, `/alive`). |
| `Logistics.Api/` | F# API (resource `logistics-api`). |
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

Tests:

```sh
dotnet test
```

## Sessions

| Date | Audience | Notes |
|------|----------|-------|
| | Internal team | |
