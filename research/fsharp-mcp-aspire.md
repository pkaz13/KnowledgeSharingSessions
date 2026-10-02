# F# support in the MCP C# SDK and Aspire

Research for issue #5 (map #4, Topic McpServerDotNet). Checked 2026-10-02.

## Answer

| Part | Language | Why |
|---|---|---|
| REST API + MCP server (one ASP.NET Core process, Streamable HTTP) | **F#** | Works with the official SDK with no wrapper library. Verified by compiling and calling it locally. |
| MCP tools and the resource | **F#** | `[<McpServerToolType>]` on a module or on a class. Both are discovered. |
| Aspire AppHost | **F# is possible; C# is the easier choice** | F# builds and runs, but you get build warning `ASPIRE001` and no generated `Projects.*` types. See the trade-off below. |
| ServiceDefaults (if you use it) | C# (from the template), or skip it | Only a C# template exists. An F# API can reference the C# project. |

Recommendation: write everything the audience reads in F# (API, tools, resource, domain). For the AppHost, choose between two options:

- **C# AppHost.** This matches the map note "C# only where F# makes no sense" and the official templates, and needs no workaround.
- **F# AppHost.** It is about 5 lines, and it shows the team the AppHost works in F# too. The cost is `AddProject(name, path)` in place of `AddProject<Projects.Api>`, plus one build warning.

Both options were verified.

## Versions (NuGet, 2026-10-02)

- `ModelContextProtocol` / `.AspNetCore` / `.Core`: **2.2.0** (published 2026-08-13; 2.0.0 GA was 2026-07-28). It targets net8.0, net9.0 and net10.0. Source: nuget.org flat-container index and nuspec, https://www.nuget.org/packages/ModelContextProtocol.AspNetCore
- `Aspire.AppHost.Sdk` / `Aspire.Hosting.AppHost`: **13.6.0**. `Aspire.Hosting.AppHost` ships net8.0, net9.0 and net10.0 assets. https://www.nuget.org/packages/Aspire.Hosting.AppHost
- Local verification ran on the .NET SDK 9.0.301 with `net9.0`. For the demo, prefer .NET 10 (LTS).

## MCP SDK with F#: evidence

### How discovery works (source)

`WithToolsFromAssembly()` selects every type that has `[McpServerToolType]`. It then reflects over `Public | NonPublic | Static | Instance` methods that carry `[McpServerTool]`. Static methods are invoked directly. For instance methods, the SDK creates the type through DI.
Source: https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.2.0/src/ModelContextProtocol/McpServerBuilderExtensions.cs (the `WithTools<T>` loop near L43 and `WithToolsFromAssembly` near L189). Resources (`WithResourcesFromAssembly` near L554) are handled the same way.

An F# `module` compiles to an abstract sealed (static) class, and a `let` function inside it compiles to a static method. Discovery therefore works without any adapter.

### Verified code shape (compiled and called over Streamable HTTP)

The sample used `Microsoft.NET.Sdk.Web` with `ModelContextProtocol.AspNetCore` 2.2.0. The API and MCP ran in one process:

```fsharp
[<McpServerToolType>]
module ShipmentTools =
    [<McpServerTool(Name = "get_shipment"); Description("Gets one shipment by id.")>]
    let getShipment (store: ShipmentStore)                          // injected from DI, not in schema
                    ([<Description("Shipment id, e.g. S-1")>] id: string) : string = ...

    [<McpServerTool(Name = "count_by_status"); Description("...")>]
    let countByStatus (store: ShipmentStore)
                      ([<Description("Status filter"); Optional; DefaultParameterValue(null: string)>] status: string) : int = ...

[<McpServerToolType>]
type DispatchTools(store: ShipmentStore) =                          // ctor DI, instance members
    [<McpServerTool(Name = "update_status"); Description("Updates shipment status.")>]
    member _.UpdateStatus([<Description("Shipment id")>] id: string, [<Description("New status")>] status: string) = ...

[<McpServerResourceType>]
module ShipmentResources =
    [<McpServerResource(UriTemplate = "logistics://depots", Name = "depots", MimeType = "text/plain"); Description("Static list of depots.")>]
    let depots () = "Oslo\nBergen\nGdansk\nHamburg"
```

```fsharp
// Program.fs
builder.Services.AddSingleton<ShipmentStore>() |> ignore
builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly().WithResourcesFromAssembly() |> ignore
let app = builder.Build()
app.MapGet("/shipments", Func<ShipmentStore, Shipment list>(fun store -> store.All())) |> ignore
app.MapMcp("/mcp") |> ignore
```

I sent raw JSON-RPC to `/mcp` with curl. Results:

| Case | Result |
|---|---|
| Module `let` function as a tool | Works |
| Class with constructor DI, instance member | Works |
| DI parameter (`store: ShipmentStore`) in a module function | Works. It is injected and left out of the input schema. |
| `[<Description>]` on a function and on its parameters | Works. Both appear in `tools/list` (`description` fields). |
| Curried `let f (a: string) (b: int)` | Works. It compiles to a 2-parameter method, and the schema has `a` and `b`. |
| `task { }` returning `Task<string>` | Works. The result is awaited. |
| **`async { }` returning `Async<string>`** | **Silent bug.** The tool returns the text `"{}"`. The SDK awaits only `Task`, `ValueTask` and `IAsyncEnumerable` (see [`AIFunctionMcpServerTool.cs`](https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.2.0/src/ModelContextProtocol.Core/Server/AIFunctionMcpServerTool.cs) near L363), so it serializes the `Async` object instead. |
| F# `option` parameter (`weight: decimal option`) | Binds correctly (`Some 5M`). The schema is `["number","null"]`, but the parameter is still listed as **required**. |
| Optional parameter via `[<Optional; DefaultParameterValue(null: string)>]` | Works. The parameter is not required, and the schema gets `"default": null`. |
| F# record / list return value | Serialized to JSON by System.Text.Json. `None` fields are omitted in MCP output (the REST endpoint emits `null`). |
| Resource from a module | `resources/list` and `resources/read` work |

### Workarounds and gotchas for F#

1. **Use `task { }`, not `async { }`**, or end with `|> Async.StartAsTask`. This is the only real trap, and it fails silently.
2. **Optional arguments.** F# `?x` and `option` do not make a parameter optional in the schema. Use `[<Optional; DefaultParameterValue(...)>]`.
3. **Descriptions.** Put `System.ComponentModel.Description` on parameters in place. No XML-doc fallback was tested.
4. **Tool names.** Set `Name = "snake_case"` explicitly. Otherwise the SDK applies its default naming policy to the .NET method name (snake_case, per `AIFunctionMcpServerTool.cs`).
5. **Nullable.** No F# 9 `--checknulls` was needed. Plain `string` with a `null` default is enough.
6. **Session mode.** In SDK 2.x, the Streamable HTTP default is **Stateless** (no `Mcp-Session-Id`). This follows the 2026-07-28 spec revision. The docs recommend setting `SessionMode` explicitly. Source: https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.2.0/docs/concepts/stateless/stateless.md. Tools plus one resource do not need sessions.

### Existing F# samples

- The official SDK repo has **no F# sample**. I grepped the v2.2.0 `samples/` and docs for F#/fsproj: none. Every sample is C#.
- F# examples that use the official SDK:
  - Oxpecker's MCP example uses a class with a `static member` and `WithTools<MyTools>()`: https://github.com/Lanayx/Oxpecker/blob/main/examples/MCP/Server/Program.fs
  - https://github.com/daniellittledev/seq-mcp (`src/SeqMcp/Tools.fs`)
  - https://github.com/fwaris/FsOperator (`FsOpMCPServer/JiraTools.fs`)

## Aspire AppHost with F#: evidence

- The AppHost MSBuild targets generate the `Projects.*` metadata classes only when `'$(Language)' == 'C#'`. For any other language they emit **warning ASPIRE001**: "The 'F#' language isn't fully supported by Aspire - some code generation targets will not run, so will require manual authoring." Source: https://github.com/microsoft/aspire/blob/v13.6.0/src/Aspire.Hosting.AppHost/build/Aspire.Hosting.AppHost.in.targets (L56, L152).
- The Aspire CLI detects `*.fsproj` as a .NET AppHost (`DetectionPatterns: ["*.csproj", "*.fsproj", "*.vbproj", "apphost.cs"]`). Source: https://github.com/microsoft/aspire/blob/v13.6.0/src/Aspire.Cli/Projects/DefaultLanguageDiscovery.cs (L30).
- The polyglot AppHosts (TypeScript, Python, plus experimental Go/Java/Rust) are a separate guest-language mechanism. F# is not among them and does not need it. See `docs/specs/polyglot-apphost.md` in the same repo.

Verified locally: an F# AppHost on `Aspire.AppHost.Sdk/13.6.0` built with only the ASPIRE001 and ASPIRE010 (CLI bundle) warnings. It started the dashboard and launched the F# API, and `/shipments` answered 200.

```fsharp
// AppHost.fsproj: <Project Sdk="Aspire.AppHost.Sdk/13.6.0"> ... <ProjectReference Include="../Api/Api.fsproj" />
open Aspire.Hosting
let builder = DistributedApplication.CreateBuilder()
builder.AddProject("logistics-api", "../Api/Api.fsproj")   // path overload: no Projects.Api type in F#
    .WithExternalHttpEndpoints() |> ignore
builder.Build().Run()
```

The C# equivalent is `builder.AddProject<Projects.Api>("logistics-api")`. It comes from the template (`aspire new`) and has no warnings.

## Open points for the build ticket

- The demo API should call `app.Run()` without a hard-coded URL, so that Aspire can assign endpoints. The local sample hard-coded a port; that was for the probe only.
- Decide whether the API uses ServiceDefaults (C# template project) or inline F# OpenTelemetry/health setup. It does not affect MCP.
