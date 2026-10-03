module Logistics.Api.Program

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http.Json
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.DependencyInjection.Extensions
open Microsoft.Extensions.Hosting
open ModelContextProtocol.AspNetCore
open Logistics.Api

/// Anchor type for WebApplicationFactory in tests.
type Marker = class end

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    builder.AddServiceDefaults() |> ignore

    builder.Services.TryAddSingleton<TimeProvider>(TimeProvider.System)

    // Reseeded on every start, dates relative to startup.
    builder.Services.AddSingleton<Store>(fun sp ->
        ref (Seed.create (sp.GetRequiredService<TimeProvider>().GetUtcNow())))
    |> ignore

    builder.Services.Configure<JsonOptions>(fun (o: JsonOptions) -> Json.configure o.SerializerOptions)
    |> ignore

    // Stateless: no Mcp-Session-Id; read tools and one resource need no server-side session.
    builder.Services
        .AddMcpServer()
        .WithHttpTransport(fun o -> o.SessionMode <- HttpServerSessionMode.Stateless)
        .WithToolsFromAssembly()
        .WithResourcesFromAssembly()
    |> ignore

    let app = builder.Build()
    app.Services.GetRequiredService<Store>() |> ignore
    app.MapDefaultEndpoints() |> ignore
    Rest.mapEndpoints app
    app.MapMcp("/mcp") |> ignore

    app.Run()
    0
